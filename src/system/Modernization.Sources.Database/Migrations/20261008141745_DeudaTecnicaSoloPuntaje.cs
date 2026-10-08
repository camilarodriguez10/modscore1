using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modernization.Sources.Database.Migrations
{
    /// <inheritdoc />
    public partial class DeudaTecnicaSoloPuntaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombinedAcceptedScore",
                table: "tech_debt");

            migrationBuilder.DropColumn(
                name: "CombinedDenialScore",
                table: "tech_debt");

            migrationBuilder.DropColumn(
                name: "LastUpdatedDate",
                table: "tech_debt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CombinedAcceptedScore",
                table: "tech_debt",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CombinedDenialScore",
                table: "tech_debt",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUpdatedDate",
                table: "tech_debt",
                type: "TEXT",
                nullable: true);
        }
    }
}
