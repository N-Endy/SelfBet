using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SelfBet.Infrastructure.Persistence;

#nullable disable

namespace SelfBet.Infrastructure.Migrations
{
    [DbContext(typeof(SelfBetDbContext))]
    [Migration("20260427190000_HistoricalDataAndTeamStrength")]
    public partial class HistoricalDataAndTeamStrength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderFixtureId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    League = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HomeTeam = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AwayTeam = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KickoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HomeGoals = table.Column<int>(type: "integer", nullable: false),
                    AwayGoals = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamStrengths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    League = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Team = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Attack = table.Column<double>(type: "double precision", nullable: false),
                    Defence = table.Column<double>(type: "double precision", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    FittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamStrengths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueStrengthProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    League = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AvgHomeGoals = table.Column<double>(type: "double precision", nullable: false),
                    AvgAwayGoals = table.Column<double>(type: "double precision", nullable: false),
                    HomeAdvantage = table.Column<double>(type: "double precision", nullable: false),
                    DixonColesRho = table.Column<double>(type: "double precision", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    FittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStrengthProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_ProviderFixtureId",
                table: "HistoricalMatches",
                column: "ProviderFixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalMatches_League_KickoffUtc",
                table: "HistoricalMatches",
                columns: new[] { "League", "KickoffUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamStrengths_League_Team",
                table: "TeamStrengths",
                columns: new[] { "League", "Team" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStrengthProfiles_League",
                table: "LeagueStrengthProfiles",
                column: "League",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HistoricalMatches");
            migrationBuilder.DropTable(name: "TeamStrengths");
            migrationBuilder.DropTable(name: "LeagueStrengthProfiles");
        }
    }
}
