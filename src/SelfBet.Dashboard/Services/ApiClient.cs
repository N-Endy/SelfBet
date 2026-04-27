using System.Net.Http.Json;
using System.Text.Json;

namespace SelfBet.Dashboard.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<StrategyConfigDto?> GetStrategyConfigAsync(CancellationToken ct = default)
    {
        var dto = await httpClient.GetFromJsonAsync<StrategyConfigDto>("/api/strategy-config/", JsonOptions, ct);
        NormalizeStrategyConfig(dto);
        return dto;
    }

    public async Task<StrategyConfigDto?> SaveStrategyConfigAsync(StrategyConfigDto config, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/strategy-config/", config, JsonOptions, ct);
        await EnsureSuccessOrThrowAsync(response, ct);
        var dto = await response.Content.ReadFromJsonAsync<StrategyConfigDto>(JsonOptions, ct);
        NormalizeStrategyConfig(dto);
        return dto;
    }

    public async Task<StrategyConfigDto?> ToggleAutomationAsync(bool enable, CancellationToken ct = default)
    {
        var url = enable ? "/api/automation/start" : "/api/automation/stop";
        var response = await httpClient.PostAsync(url, null, ct);
        await EnsureSuccessOrThrowAsync(response, ct);
        var dto = await response.Content.ReadFromJsonAsync<StrategyConfigDto>(JsonOptions, ct);
        NormalizeStrategyConfig(dto);
        return dto;
    }

    public async Task<RunOutcomeDto?> ExecuteRunAsync(CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("/api/runs/execute-now", null, ct);
        await EnsureSuccessOrThrowAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RunOutcomeDto>(JsonOptions, ct);
    }

    private static void NormalizeStrategyConfig(StrategyConfigDto? c)
    {
        if (c is null) return;
        if (c.EnabledLeagues is { Count: 0 } && !string.IsNullOrWhiteSpace(c.EnabledLeaguesCsv))
        {
            c.EnabledLeagues = c.EnabledLeaguesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        if (c.AllowedMarkets is { Count: 0 } && !string.IsNullOrWhiteSpace(c.AllowedMarketsCsv))
        {
            c.AllowedMarkets = c.AllowedMarketsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"API error {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"API {(int)response.StatusCode}: {body}");
    }

    public Task<List<SlipDto>?> GetTodaySlipsAsync(CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<List<SlipDto>>("/api/slips/today", JsonOptions, ct);

    public async Task<SlipActionClientResult> PlaceSlipAsync(Guid slipId, CancellationToken ct = default) =>
        await ReadSlipActionResultAsync(
            await httpClient.PostAsync($"/api/slips/{slipId}/place", null, ct), ct);

    public async Task<SlipActionClientResult> CancelSlipAsync(Guid slipId, CancellationToken ct = default) =>
        await ReadSlipActionResultAsync(
            await httpClient.PostAsync($"/api/slips/{slipId}/cancel", null, ct), ct);

    private static async Task<SlipActionClientResult> ReadSlipActionResultAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        var message = TryGetJsonMessageProperty(text);
        if (string.IsNullOrEmpty(message) && !string.IsNullOrWhiteSpace(text) && !response.IsSuccessStatusCode)
        {
            message = text;
        }
        if (string.IsNullOrEmpty(message) && !response.IsSuccessStatusCode)
        {
            message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        return new SlipActionClientResult(response.IsSuccessStatusCode, message);
    }

    private static string? TryGetJsonMessageProperty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var d = JsonDocument.Parse(json);
            if (d.RootElement.ValueKind == JsonValueKind.Object
                && d.RootElement.TryGetProperty("message", out var p)
                && p.ValueKind == JsonValueKind.String)
            {
                return p.GetString();
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    public Task<BankrollSnapshotDto?> GetBankrollAsync(CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<BankrollSnapshotDto>("/api/bankroll/current", JsonOptions, ct);

    public async Task<BankrollSnapshotDto?> CaptureBankrollAsync(decimal balance, string? note, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/bankroll/snapshot", new { balance, note }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BankrollSnapshotDto>(JsonOptions, ct);
    }

    public Task<PerformanceSummaryDto?> GetPerformanceSummaryAsync(int days, CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<PerformanceSummaryDto>($"/api/performance/summary?rangeDays={days}", JsonOptions, ct);

    public Task<List<SlipDto>?> GetPerformanceHistoryAsync(int limit, CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<List<SlipDto>>($"/api/performance/history?limit={limit}", JsonOptions, ct);

    public Task<List<RunDto>?> GetRunsAsync(int limit, CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<List<RunDto>>($"/api/runs/?limit={limit}", JsonOptions, ct);

    public Task<List<AuditEventDto>?> GetAuditEventsAsync(int limit, CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<List<AuditEventDto>>($"/api/audit-events/?limit={limit}", JsonOptions, ct);

    public async Task<bool> SubmitOtpAsync(string otp, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/automation/otp", new { otp }, JsonOptions, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<decimal?> GetLiveBalanceAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/api/bankroll/live", ct);
            if (!response.IsSuccessStatusCode) return null;
            var dto = await response.Content.ReadFromJsonAsync<BankrollSnapshotDto>(JsonOptions, ct);
            return dto?.Balance;
        }
        catch { return null; }
    }
}
