using System.Net.Http.Json;
using System.Text.Json;

namespace SelfBet.Dashboard.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<StrategyConfigDto?> GetStrategyConfigAsync(CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<StrategyConfigDto>("/api/strategy-config/", JsonOptions, ct);

    public async Task<StrategyConfigDto?> SaveStrategyConfigAsync(StrategyConfigDto config, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/strategy-config/", config, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StrategyConfigDto>(JsonOptions, ct);
    }

    public async Task<StrategyConfigDto?> ToggleAutomationAsync(bool enable, CancellationToken ct = default)
    {
        var url = enable ? "/api/automation/start" : "/api/automation/stop";
        var response = await httpClient.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StrategyConfigDto>(JsonOptions, ct);
    }

    public async Task<RunOutcomeDto?> ExecuteRunAsync(CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("/api/runs/execute-now", null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RunOutcomeDto>(JsonOptions, ct);
    }

    public Task<List<SlipDto>?> GetTodaySlipsAsync(CancellationToken ct = default) =>
        httpClient.GetFromJsonAsync<List<SlipDto>>("/api/slips/today", JsonOptions, ct);

    public async Task<bool> PlaceSlipAsync(Guid slipId, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync($"/api/slips/{slipId}/place", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CancelSlipAsync(Guid slipId, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync($"/api/slips/{slipId}/cancel", null, ct);
        return response.IsSuccessStatusCode;
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
