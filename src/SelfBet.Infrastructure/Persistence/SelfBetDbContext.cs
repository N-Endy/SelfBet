using Microsoft.EntityFrameworkCore;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class SelfBetDbContext(DbContextOptions<SelfBetDbContext> options) : DbContext(options)
{
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<Slip> Slips => Set<Slip>();
    public DbSet<SlipLeg> SlipLegs => Set<SlipLeg>();
    public DbSet<PlacementAttempt> PlacementAttempts => Set<PlacementAttempt>();
    public DbSet<BankrollSnapshot> BankrollSnapshots => Set<BankrollSnapshot>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<StrategyConfig> StrategyConfigs => Set<StrategyConfig>();
    public DbSet<ForecastObservation> ForecastObservations => Set<ForecastObservation>();
    public DbSet<CalibrationProfile> CalibrationProfiles => Set<CalibrationProfile>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Run ────────────────────────────────────────────────────────────────
        mb.Entity<Run>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Trigger).HasMaxLength(64);
            e.Property(x => x.Message).HasMaxLength(1024);
        });

        // ── Slip ───────────────────────────────────────────────────────────────
        mb.Entity<Slip>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.PotentialReturn);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.FailureReason).HasMaxLength(512);
            e.Property(x => x.BookingCode).HasMaxLength(32);
            e.Property(x => x.BookingUrl).HasMaxLength(512);
            e.Property(x => x.ExternalTicketId).HasMaxLength(128);
            e.HasMany(x => x.Legs).WithOne().HasForeignKey(l => l.SlipId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── SlipLeg ────────────────────────────────────────────────────────────
        mb.Entity<SlipLeg>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MatchTitle).HasMaxLength(256);
            e.Property(x => x.League).HasMaxLength(128);
            e.Property(x => x.Market).HasMaxLength(64);
            e.Property(x => x.Outcome).HasMaxLength(64);
        });

        // ── PlacementAttempt ──────────────────────────────────────────────────
        mb.Entity<PlacementAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BookingCode).HasMaxLength(32);
            e.Property(x => x.BookingUrl).HasMaxLength(512);
            e.Property(x => x.ExternalTicketId).HasMaxLength(128);
            e.Property(x => x.Error).HasMaxLength(1024);
            e.Property(x => x.PlacementMode).HasMaxLength(32);
        });

        // ── BankrollSnapshot ──────────────────────────────────────────────────
        mb.Entity<BankrollSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Note).HasMaxLength(256);
        });

        // ── AuditEvent ────────────────────────────────────────────────────────
        mb.Entity<AuditEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(128);
            e.Property(x => x.Message).HasMaxLength(1024);
        });

        // ── StrategyConfig (singleton row Id=1) ──────────────────────────────
        mb.Entity<StrategyConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.OddsRange);
            e.Ignore(x => x.EnabledLeagues);
            e.Ignore(x => x.AllowedMarkets);
            e.Property(x => x.EnabledLeaguesCsv).HasMaxLength(2048);
            e.Property(x => x.AllowedMarketsCsv).HasMaxLength(512);
        });

        // ── ForecastObservation (for calibration) ────────────────────────────
        mb.Entity<ForecastObservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Market).HasMaxLength(64);
            e.Property(x => x.Outcome).HasMaxLength(64);
            e.Property(x => x.League).HasMaxLength(128);
            e.HasIndex(x => new { x.Market, x.ResolvedAtUtc });
        });

        // ── CalibrationProfile ────────────────────────────────────────────────
        mb.Entity<CalibrationProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Market).HasMaxLength(64);
            e.HasIndex(x => x.Market).IsUnique();
        });
    }
}
