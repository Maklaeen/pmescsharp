using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmesCSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlanDefinitionsAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: "CompanySubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SubscriptionGlobalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayMongoPublicKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PayMongoSecretKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionGlobalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MonthlyPriceCentavos = table.Column<long>(type: "bigint", nullable: false),
                    AnnualPriceCentavos = table.Column<long>(type: "bigint", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxProducts = table.Column<int>(type: "int", nullable: false),
                    MaxMaterials = table.Column<int>(type: "int", nullable: false),
                    MaxWorkOrdersPerMonth = table.Column<int>(type: "int", nullable: false),
                    MaxStorageMb = table.Column<int>(type: "int", nullable: false),
                    EnableReports = table.Column<bool>(type: "bit", nullable: false),
                    EnableCosting = table.Column<bool>(type: "bit", nullable: false),
                    EnableAuditLogs = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanDefinitions_Plan",
                table: "SubscriptionPlanDefinitions",
                column: "Plan",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionGlobalSettings");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanDefinitions");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "CompanySubscriptions");
        }
    }
}
