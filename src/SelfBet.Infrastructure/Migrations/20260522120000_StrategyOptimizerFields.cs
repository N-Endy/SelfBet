using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfBet.Infrastructure.Migrations;

public partial class StrategyOptimizerFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FixtureLookaheadHours",
            table: "StrategyConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 48);

        migrationBuilder.AddColumn<int>(
            name: "OptimizerBeamWidth",
            table: "StrategyConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 12);

        migrationBuilder.AddColumn<bool>(
            name: "PreferDiversification",
            table: "StrategyConfigs",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MinModelProbability",
            table: "StrategyConfigs",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FixtureLookaheadHours", table: "StrategyConfigs");
        migrationBuilder.DropColumn(name: "OptimizerBeamWidth", table: "StrategyConfigs");
        migrationBuilder.DropColumn(name: "PreferDiversification", table: "StrategyConfigs");
        migrationBuilder.DropColumn(name: "MinModelProbability", table: "StrategyConfigs");
    }
}
