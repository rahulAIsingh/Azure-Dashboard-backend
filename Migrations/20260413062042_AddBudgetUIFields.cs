using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureFinOps.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetUIFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertAt",
                table: "Budgets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Budgets",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Budgets");
        }
    }
}
