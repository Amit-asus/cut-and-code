using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalonImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Salons",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Salons");
        }
    }
}
