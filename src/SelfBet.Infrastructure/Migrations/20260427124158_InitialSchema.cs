using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfBet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankrollSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric", nullable: false),
                    StakePerSlip = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Note = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankrollSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Market = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slope = table.Column<double>(type: "double precision", nullable: false),
                    Intercept = table.Column<double>(type: "double precision", nullable: false),
                    PublishThreshold = table.Column<double>(type: "double precision", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForecastObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlipLegId = table.Column<Guid>(type: "uuid", nullable: false),
                    Market = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    League = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ModelProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    BookOdds = table.Column<decimal>(type: "numeric", nullable: false),
                    Correct = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlacementAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalTicketId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BookingCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BookingUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EvidencePath = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PlacementMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FixturesEvaluated = table.Column<int>(type: "integer", nullable: false),
                    CandidatesGenerated = table.Column<int>(type: "integer", nullable: false),
                    SlipsBuilt = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Slips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Stake = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalOdds = table.Column<decimal>(type: "numeric", nullable: false),
                    Payout = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BookingCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BookingUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExternalTicketId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OddsRangeMin = table.Column<decimal>(type: "numeric", nullable: false),
                    OddsRangeMax = table.Column<decimal>(type: "numeric", nullable: false),
                    StakePercentagePerSlip = table.Column<decimal>(type: "numeric", nullable: false),
                    SlipsPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxLegsPerSlip = table.Column<int>(type: "integer", nullable: false),
                    MinLegsPerSlip = table.Column<int>(type: "integer", nullable: false),
                    MinEdgeThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    MinLegOdds = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxLegOdds = table.Column<decimal>(type: "numeric", nullable: false),
                    StakeIncrement = table.Column<decimal>(type: "numeric", nullable: false),
                    AutomationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RequireConfirmationOnRisk = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnabledLeaguesCsv = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AllowedMarketsCsv = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlipLegs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    League = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Market = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Odds = table.Column<decimal>(type: "numeric", nullable: false),
                    ModelProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    KickoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlipLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlipLegs_Slips_SlipId",
                        column: x => x.SlipId,
                        principalTable: "Slips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationProfiles_Market",
                table: "CalibrationProfiles",
                column: "Market",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastObservations_Market_ResolvedAtUtc",
                table: "ForecastObservations",
                columns: new[] { "Market", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SlipLegs_SlipId",
                table: "SlipLegs",
                column: "SlipId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "BankrollSnapshots");

            migrationBuilder.DropTable(
                name: "CalibrationProfiles");

            migrationBuilder.DropTable(
                name: "ForecastObservations");

            migrationBuilder.DropTable(
                name: "PlacementAttempts");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "SlipLegs");

            migrationBuilder.DropTable(
                name: "StrategyConfigs");

            migrationBuilder.DropTable(
                name: "Slips");
        }
    }
}
