using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FifthBox.ServerManager.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RouteMaxBodySize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxBodySizeMb",
                table: "Routes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxBodySizeMb",
                table: "Routes");
        }
    }
}
