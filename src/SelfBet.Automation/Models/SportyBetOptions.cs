namespace SelfBet.Automation.Models;

public sealed class SportyBetOptions
{
    public const string Section = "SportyBet";

    public string BaseUrl { get; set; } = "https://www.sportybet.com";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// Placement mode: "booking_code" (default) or "full_auth".
    /// booking_code = share code generated, no stake taken automatically.
    /// full_auth     = logs in and places the bet immediately.
    /// </summary>
    public string PlacementMode { get; set; } = "booking_code";

    public bool DryRun { get; set; } = false;

    /// <summary>Milliseconds to wait between selecting legs (reduce bot detection risk).</summary>
    public int PacingDelayMs { get; set; } = 800;
}
