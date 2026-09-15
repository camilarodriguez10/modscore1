using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modernization.Sources.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReglasVersionadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rules_version",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BasedOn = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Ruleset = table.Column<string>(type: "TEXT", nullable: false),
                    Levers = table.Column<string>(type: "TEXT", nullable: false),
                    Roadmap = table.Column<string>(type: "TEXT", nullable: false),
                    Changes = table.Column<string>(type: "TEXT", nullable: false),
                    Impact = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rules_version", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rules_version");
        }
    }
}
