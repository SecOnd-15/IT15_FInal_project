using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Latog_Final_project.Migrations
{
    /// <inheritdoc />
    public partial class AddFullNameToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ResourceRequestId",
                table: "Invoices",
                column: "ResourceRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_ResourceRequests_ResourceRequestId",
                table: "Invoices",
                column: "ResourceRequestId",
                principalTable: "ResourceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_ResourceRequests_ResourceRequestId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ResourceRequestId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");
        }
    }
}
