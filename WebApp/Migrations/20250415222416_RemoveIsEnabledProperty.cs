using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReverseProxy.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsEnabledProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Mappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Mappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "Mappings",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsEnabled",
                value: true);
        }
    }
}
