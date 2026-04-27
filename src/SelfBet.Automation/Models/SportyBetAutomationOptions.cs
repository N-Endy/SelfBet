namespace SelfBet.Automation.Models;

public sealed class SportyBetAutomationOptions
{
    public string Region { get; init; } = "ng";
    public string BaseUrl { get; init; } = "https://www.sportybet.com/ng/";
    public string EvidenceDirectory { get; init; } = "artifacts/placements";
    public bool Headless { get; init; }
    public bool DryRun { get; init; } = true;
    public int MinDelayMs { get; init; } = 800;
    public int MaxDelayMs { get; init; } = 2200;
    public string? UsernameSecretName { get; init; }
    public string? PasswordSecretName { get; init; }
}
