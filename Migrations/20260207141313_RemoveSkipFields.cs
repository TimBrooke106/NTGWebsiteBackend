using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkipHire.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSkipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialType",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SkipSize",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialType",
                table: "Bookings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkipSize",
                table: "Bookings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
