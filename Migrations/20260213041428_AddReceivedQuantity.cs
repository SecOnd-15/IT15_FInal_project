using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Latog_Final_project.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "ResourceRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "ResourceRequests");
        }
    }
}
