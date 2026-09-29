using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErshadReferral.Migrations
{
    /// <inheritdoc />
    public partial class AddArrivalTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalTime",
                table: "VisitorEntries",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTime",
                table: "VisitorEntries");
        }
    }
}
