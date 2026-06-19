using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdnFileService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "CDN.Files",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "CDN.AppUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDN.Files_CompanyId",
                table: "CDN.Files",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CDN.AppUsers_CompanyId",
                table: "CDN.AppUsers",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CDN.Files_CompanyId",
                table: "CDN.Files");

            migrationBuilder.DropIndex(
                name: "IX_CDN.AppUsers_CompanyId",
                table: "CDN.AppUsers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CDN.Files");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CDN.AppUsers");
        }
    }
}
