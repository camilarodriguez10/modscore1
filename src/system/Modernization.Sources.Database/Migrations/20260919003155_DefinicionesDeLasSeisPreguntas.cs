using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modernization.Sources.Database.Migrations
{
    /// <inheritdoc />
    public partial class DefinicionesDeLasSeisPreguntas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Analytics",
                table: "rules_version",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Analytics",
                table: "rules_version");
        }
    }
}
