using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Latog_Final_project.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardedByToResourceRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForwardedByUserId",
                table: "ResourceRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequests_ForwardedByUserId",
                table: "ResourceRequests",
                column: "ForwardedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceRequests_AspNetUsers_ForwardedByUserId",
                table: "ResourceRequests",
                column: "ForwardedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceRequests_AspNetUsers_ForwardedByUserId",
                table: "ResourceRequests");

            migrationBuilder.DropIndex(
                name: "IX_ResourceRequests_ForwardedByUserId",
                table: "ResourceRequests");

            migrationBuilder.DropColumn(
                name: "ForwardedByUserId",
                table: "ResourceRequests");
        }
    }
}
