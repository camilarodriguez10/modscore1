using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modernization.Sources.Database.Migrations
{
    /// <inheritdoc />
    public partial class MapeoDeColumnas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColumnMaps",
                table: "snapshot_version",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColumnMaps",
                table: "snapshot_version");
        }
    }
}
