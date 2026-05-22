using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SelfBet.Infrastructure.Persistence;

#nullable disable

namespace SelfBet.Infrastructure.Migrations;

[DbContext(typeof(SelfBetDbContext))]
[Migration("20260522120000_StrategyOptimizerFields")]
public partial class StrategyOptimizerFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "StrategyConfigs" ADD COLUMN IF NOT EXISTS "FixtureLookaheadHours" integer NOT NULL DEFAULT 48;
            ALTER TABLE "StrategyConfigs" ADD COLUMN IF NOT EXISTS "OptimizerBeamWidth" integer NOT NULL DEFAULT 12;
            ALTER TABLE "StrategyConfigs" ADD COLUMN IF NOT EXISTS "PreferDiversification" boolean NOT NULL DEFAULT true;
            ALTER TABLE "StrategyConfigs" ADD COLUMN IF NOT EXISTS "MinModelProbability" numeric NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "StrategyConfigs" DROP COLUMN IF EXISTS "FixtureLookaheadHours";
            ALTER TABLE "StrategyConfigs" DROP COLUMN IF EXISTS "OptimizerBeamWidth";
            ALTER TABLE "StrategyConfigs" DROP COLUMN IF EXISTS "PreferDiversification";
            ALTER TABLE "StrategyConfigs" DROP COLUMN IF EXISTS "MinModelProbability";
            """);
    }
}
