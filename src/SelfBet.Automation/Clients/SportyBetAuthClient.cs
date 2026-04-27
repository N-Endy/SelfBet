using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SelfBet.Automation.Models;

namespace SelfBet.Automation.Clients;

/// <summary>
/// Manages an authenticated SportyBet session.
/// Flow: login (phone + password) → persist cookies → fetch balance → place bet.
/// If an OTP challenge is returned, raises OtpRequiredEvent; the dashboard
/// can POST the code via /api/automation/otp to unblock the pending placement.
/// </summary>
public sealed class SportyBetAuthClient(
    SportyBetOptions options,
    ILogger<SportyBetAuthClient> logger)
{
    private readonly CookieContainer _cookies = new();
    private string? _token;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private string? _pendingOtpToken; // server's OTP challenge token
    private bool _isLoggedIn;

    public bool IsLoggedIn => _isLoggedIn && _tokenExpiry > DateTimeOffset.UtcNow;
    public event Func<string, Task>? OtpRequired;

    public async Task<LoginResult> LoginAsync(CancellationToken ct = default)
    {
        if (IsLoggedIn)
            return new LoginResult { Success = true };

        var client = CreateClient();
        var phone = options.Phone;
        var password = options.Password;

        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            return new LoginResult { Success = false, Message = "SportyBet credentials not configured." };

        // Step 1: POST /api/ng/users/mobileLogin
        var payload = JsonSerializer.Serialize(new
        {
            phone_no = phone.TrimStart('+'),
            password,
            source = 0
        });

        var body = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync($"{options.BaseUrl}/api/ng/users/mobileLogin", body, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            logger.LogInformation("Login response [{Status}]: {Body}",
                response.StatusCode, responseBody[..Math.Min(400, responseBody.Length)]);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Check for OTP challenge
            if (root.TryGetProperty("bizCode", out var biz) && biz.GetInt32() == 10035)
            {
                // OTP required
                if (root.TryGetProperty("data", out var otpData) &&
                    otpData.TryGetProperty("token", out var otpToken))
                    _pendingOtpToken = otpToken.GetString();

                if (OtpRequired is not null)
                    await OtpRequired.Invoke("SportyBet requires OTP verification. Please check your SMS and enter the code in the dashboard.");

                return new LoginResult { Success = false, RequiresOtp = true, Message = "OTP sent to your phone." };
            }

            if (!response.IsSuccessStatusCode)
                return new LoginResult { Success = false, Message = $"Login failed: HTTP {response.StatusCode}" };

            // Extract token
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("token", out var tokenEl)) _token = tokenEl.GetString();
                else if (data.TryGetProperty("accessToken", out var at)) _token = at.GetString();
            }

            if (!string.IsNullOrEmpty(_token))
            {
                _isLoggedIn = true;
                _tokenExpiry = DateTimeOffset.UtcNow.AddHours(12);
                logger.LogInformation("SportyBet login successful.");
                return new LoginResult { Success = true };
            }

            return new LoginResult { Success = false, Message = "Login response did not include a token." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SportyBet login error");
            return new LoginResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<LoginResult> SubmitOtpAsync(string otp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_pendingOtpToken))
            return new LoginResult { Success = false, Message = "No pending OTP challenge." };

        var client = CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            phone_no = options.Phone.TrimStart('+'),
            sms_code = otp.Trim(),
            token = _pendingOtpToken
        });

        var body = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync($"{options.BaseUrl}/api/ng/users/verifyPhone", body, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("token", out var tokenEl)) _token = tokenEl.GetString();
                else if (data.TryGetProperty("accessToken", out var at)) _token = at.GetString();
            }

            if (!string.IsNullOrEmpty(_token))
            {
                _isLoggedIn = true;
                _tokenExpiry = DateTimeOffset.UtcNow.AddHours(12);
                _pendingOtpToken = null;
                logger.LogInformation("OTP verification successful.");
                return new LoginResult { Success = true };
            }

            return new LoginResult { Success = false, Message = "OTP verification failed." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OTP submission error");
            return new LoginResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<decimal?> GetBalanceAsync(CancellationToken ct = default)
    {
        if (!IsLoggedIn && (await LoginAsync(ct)).Success == false) return null;

        var client = CreateAuthenticatedClient();
        try
        {
            var response = await client.GetAsync($"{options.BaseUrl}/api/ng/users/myInfo", ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                // Balance may be in data.balance, data.wallet.balance, or data.availableBalance
                foreach (var key in new[] { "balance", "availableBalance" })
                {
                    if (data.TryGetProperty(key, out var b))
                    {
                        if (b.ValueKind == JsonValueKind.Number && b.TryGetDecimal(out var bal))
                            return bal;
                        if (b.ValueKind == JsonValueKind.String &&
                            decimal.TryParse(b.GetString(), out var sbal))
                            return sbal;
                    }
                }

                if (data.TryGetProperty("wallet", out var wallet))
                    if (wallet.TryGetProperty("balance", out var wb) && wb.TryGetDecimal(out var wbal))
                        return wbal;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch balance");
            return null;
        }
    }

    /// <summary>
    /// Places a bet using the authenticated orders API.
    /// This takes money from your account immediately.
    /// </summary>
    public async Task<PlaceOrderResult> PlaceOrderAsync(
        IReadOnlyList<SportyBetSelection> selections,
        decimal stake,
        CancellationToken ct = default)
    {
        if (!IsLoggedIn)
        {
            var login = await LoginAsync(ct);
            if (!login.Success)
                return new PlaceOrderResult { Success = false, Message = login.Message };
            if (login.RequiresOtp)
                return new PlaceOrderResult { Success = false, RequiresOtp = true, Message = "OTP required before placing order." };
        }

        var client = CreateAuthenticatedClient();

        // SportyBet order payload (reverse-engineered from web app network traffic)
        var payload = new
        {
            selectionsMap = selections.Select(s => new
            {
                eventId = s.EventId,
                marketId = s.MarketId,
                specifier = s.Specifier,
                outcomeId = s.OutcomeId
            }).ToArray(),
            amount = (double)stake,
            source = 0
        };

        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync($"{options.BaseUrl}/api/ng/orders/create", body, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            logger.LogInformation("PlaceOrder [{Status}]: {Body}",
                response.StatusCode, responseBody[..Math.Min(500, responseBody.Length)]);

            if (!response.IsSuccessStatusCode)
                return new PlaceOrderResult { Success = false, Message = $"HTTP {response.StatusCode}" };

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("bizCode", out var biz) && biz.GetInt32() == 10000)
            {
                string? ticketId = null;
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("orderId", out var oid)) ticketId = oid.GetString();
                    else if (data.TryGetProperty("betId", out var bid)) ticketId = bid.GetString();
                    else if (data.ValueKind == JsonValueKind.String) ticketId = data.GetString();
                }

                return new PlaceOrderResult { Success = true, TicketId = ticketId };
            }

            var errMsg = root.TryGetProperty("msg", out var msg) ? msg.GetString() : "Unknown error";
            return new PlaceOrderResult { Success = false, Message = errMsg };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlaceOrder error");
            return new PlaceOrderResult { Success = false, Message = ex.Message };
        }
    }

    public void Logout()
    {
        _isLoggedIn = false;
        _token = null;
        _tokenExpiry = DateTimeOffset.MinValue;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
        var client = new HttpClient(handler);
        AddBaseHeaders(client);
        return client;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        if (!string.IsNullOrEmpty(_token))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_token}");
        return client;
    }

    private void AddBaseHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{options.BaseUrl}/ng/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", options.BaseUrl);
        client.DefaultRequestHeaders.TryAddWithoutValidation("clientid", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("platform", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("operid", "2");
        client.Timeout = TimeSpan.FromSeconds(30);
    }
}

// ── Supporting types ─────────────────────────────────────────────────────

public sealed class LoginResult
{
    public bool Success { get; init; }
    public bool RequiresOtp { get; init; }
    public string? Message { get; init; }
}

public sealed class PlaceOrderResult
{
    public bool Success { get; init; }
    public bool RequiresOtp { get; init; }
    public string? TicketId { get; init; }
    public string? Message { get; init; }
}
