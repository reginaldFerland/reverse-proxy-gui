using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReverseProxy.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RoutePattern = table.Column<string>(type: "TEXT", nullable: false),
                    Destination1 = table.Column<string>(type: "TEXT", nullable: false),
                    Destination2 = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveDestination = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mappings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Mappings",
                columns: new[] { "Id", "ActiveDestination", "Destination1", "Destination2", "Name", "RoutePattern" },
                values: new object[] { 1, 1, "https://api1.example.com", "https://api2.example.com", "Default Route", "/api/{**catch-all}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mappings");
        }
    }
}
