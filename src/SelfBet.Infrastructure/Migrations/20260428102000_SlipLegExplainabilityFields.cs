using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfBet.Infrastructure.Migrations
{
    public partial class SlipLegExplainabilityFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Edge",
                table: "SlipLegs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedValue",
                table: "SlipLegs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AwaySampleSize",
                table: "SlipLegs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeSampleSize",
                table: "SlipLegs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketImpliedProbability",
                table: "SlipLegs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PredictionSource",
                table: "SlipLegs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "BookmakerFallback");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Edge", table: "SlipLegs");
            migrationBuilder.DropColumn(name: "ExpectedValue", table: "SlipLegs");
            migrationBuilder.DropColumn(name: "AwaySampleSize", table: "SlipLegs");
            migrationBuilder.DropColumn(name: "HomeSampleSize", table: "SlipLegs");
            migrationBuilder.DropColumn(name: "MarketImpliedProbability", table: "SlipLegs");
            migrationBuilder.DropColumn(name: "PredictionSource", table: "SlipLegs");
        }
    }
}
