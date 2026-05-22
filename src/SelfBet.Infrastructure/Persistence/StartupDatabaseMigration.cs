using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SelfBet.Infrastructure.Persistence;

/// <summary>
/// Applies EF migrations and reconciles history when columns were created outside EF (e.g. prior startup SQL).
/// </summary>
public static class StartupDatabaseMigration
{
    private const string ProductVersion = "10.0.0";

    public static async Task ApplyAsync(SelfBetDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await ReconcileOrphanedSchemaAsync(db, logger, ct);
        await db.Database.MigrateAsync(ct);
    }

    private static async Task ReconcileOrphanedSchemaAsync(
        SelfBetDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260428102000_SlipLegExplainabilityFields', '{ProductVersion}'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260428102000_SlipLegExplainabilityFields')
              AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'SlipLegs' AND column_name = 'Edge');

            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260522120000_StrategyOptimizerFields', '{ProductVersion}'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260522120000_StrategyOptimizerFields')
              AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'StrategyConfigs' AND column_name = 'FixtureLookaheadHours');
            """,
            ct);

        logger.LogDebug("Migration history reconciliation completed (if needed).");
    }
}
