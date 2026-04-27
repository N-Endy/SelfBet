namespace SelfBet.Dashboard.Services;

public sealed class StrategyConfigDto
{
    public decimal OddsRangeMin { get; set; } = 6m;
    public decimal OddsRangeMax { get; set; } = 10m;
    public decimal StakePercentagePerSlip { get; set; }
    public int SlipsPerDay { get; set; }
    public int MaxLegsPerSlip { get; set; }
    public int MinLegsPerSlip { get; set; }
    public decimal MinEdgeThreshold { get; set; }
    public decimal MinLegOdds { get; set; }
    public decimal MaxLegOdds { get; set; }
    public decimal StakeIncrement { get; set; }
    public bool AutomationEnabled { get; set; }
    public bool RequireConfirmationOnRisk { get; set; }
    public List<string> EnabledLeagues { get; set; } = [];
    public List<string> AllowedMarkets { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>EF/API wire format — <see cref="EnabledLeagues"/> is derived when lists are empty.</summary>
    public string? EnabledLeaguesCsv { get; set; }

    /// <summary>EF/API wire format — <see cref="AllowedMarkets"/> is derived when lists are empty.</summary>
    public string? AllowedMarketsCsv { get; set; }
}

public sealed class OddsRangeDto
{
    public decimal Min { get; set; } = 6m;
    public decimal Max { get; set; } = 10m;
}

public sealed class SlipDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public DateOnly RunDate { get; set; }
    public int Sequence { get; set; }
    public decimal Stake { get; set; }
    public decimal TotalOdds { get; set; }
    public decimal PotentialReturn { get; set; }
    public decimal Payout { get; set; }
    public string Status { get; set; } = "Planned";
    public string? FailureReason { get; set; }
    public string? BookingCode { get; set; }
    public string? BookingUrl { get; set; }
    public string? ExternalTicketId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<SlipLegDto> Legs { get; set; } = [];
}

public sealed class SlipLegDto
{
    public string MatchTitle { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public decimal Odds { get; set; }
    public decimal ModelProbability { get; set; }
    public DateTimeOffset KickoffUtc { get; set; }
}

public sealed class RunDto
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "Planned";
    public string Trigger { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int FixturesEvaluated { get; set; }
    public int CandidatesGenerated { get; set; }
    public int SlipsBuilt { get; set; }
}

public sealed class RunOutcomeDto
{
    public RunDto Run { get; set; } = new();
    public List<SlipDto> Slips { get; set; } = [];
}

public sealed class BankrollSnapshotDto
{
    public Guid Id { get; set; }
    public decimal Balance { get; set; }
    public decimal StakePerSlip { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string? Note { get; set; }
}

public sealed class PerformanceSummaryDto
{
    public int TotalSlips { get; set; }
    public int Won { get; set; }
    public int Lost { get; set; }
    public int Pending { get; set; }
    public decimal TotalStake { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal NetProfit { get; set; }
    public decimal Roi { get; set; }
    public decimal HitRate { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
}

public sealed class AuditEventDto
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
}
