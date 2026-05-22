using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SelfBet.Infrastructure.Persistence;

#nullable disable

namespace SelfBet.Infrastructure.Migrations
{
    [DbContext(typeof(SelfBetDbContext))]
    [Migration("20260428102000_SlipLegExplainabilityFields")]
    public partial class SlipLegExplainabilityFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "Edge" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "ExpectedValue" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "AwaySampleSize" integer NULL;
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "HomeSampleSize" integer NULL;
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "MarketImpliedProbability" numeric NOT NULL DEFAULT 0;
                ALTER TABLE "SlipLegs" ADD COLUMN IF NOT EXISTS "PredictionSource" character varying(32) NOT NULL DEFAULT 'BookmakerFallback';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "Edge";
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "ExpectedValue";
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "AwaySampleSize";
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "HomeSampleSize";
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "MarketImpliedProbability";
                ALTER TABLE "SlipLegs" DROP COLUMN IF EXISTS "PredictionSource";
                """);
        }
    }
}
