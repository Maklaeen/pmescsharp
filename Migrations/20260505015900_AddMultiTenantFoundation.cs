using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmesCSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_WorkOrderNo",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Materials_MaterialCode",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_BillOfMaterials_ProductId_MaterialId",
                table: "BillOfMaterials");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "WorkOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "QualityChecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ProductionSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ProductionCosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Materials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "MaterialMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "BillOfMaterials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            // Backfill CompanyId for existing rows so foreign keys can be added safely.
            migrationBuilder.Sql(@"
DECLARE @CompanyId int;

INSERT INTO [Companies] ([Name], [Code], [CreatedAt], [UpdatedAt])
VALUES (N'Default Company', N'default', SYSUTCDATETIME(), SYSUTCDATETIME());

SET @CompanyId = CAST(SCOPE_IDENTITY() AS int);

UPDATE [Products] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [Materials] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [BillOfMaterials] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [ProductionSchedules] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [WorkOrders] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [MaterialMovements] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [QualityChecks] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;
UPDATE [ProductionCosts] SET [CompanyId] = @CompanyId WHERE [CompanyId] = 0;

UPDATE [AspNetUsers] SET [CompanyId] = @CompanyId WHERE [CompanyId] IS NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CompanyId_WorkOrderNo",
                table: "WorkOrders",
                columns: new[] { "CompanyId", "WorkOrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_CompanyId",
                table: "QualityChecks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_ProductCode",
                table: "Products",
                columns: new[] { "CompanyId", "ProductCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSchedules_CompanyId_Status_ScheduleDate",
                table: "ProductionSchedules",
                columns: new[] { "CompanyId", "Status", "ScheduleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCosts_CompanyId",
                table: "ProductionCosts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CompanyId_MaterialCode",
                table: "Materials",
                columns: new[] { "CompanyId", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMovements_CompanyId",
                table: "MaterialMovements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterials_CompanyId_ProductId_MaterialId",
                table: "BillOfMaterials",
                columns: new[] { "CompanyId", "ProductId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterials_ProductId",
                table: "BillOfMaterials",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Code",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BillOfMaterials_Companies_CompanyId",
                table: "BillOfMaterials",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialMovements_Companies_CompanyId",
                table: "MaterialMovements",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Companies_CompanyId",
                table: "Materials",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionCosts_Companies_CompanyId",
                table: "ProductionCosts",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionSchedules_Companies_CompanyId",
                table: "ProductionSchedules",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QualityChecks_Companies_CompanyId",
                table: "QualityChecks",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Companies_CompanyId",
                table: "WorkOrders",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BillOfMaterials_Companies_CompanyId",
                table: "BillOfMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialMovements_Companies_CompanyId",
                table: "MaterialMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Companies_CompanyId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionCosts_Companies_CompanyId",
                table: "ProductionCosts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionSchedules_Companies_CompanyId",
                table: "ProductionSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_QualityChecks_Companies_CompanyId",
                table: "QualityChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Companies_CompanyId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CompanyId_WorkOrderNo",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_QualityChecks_CompanyId",
                table: "QualityChecks");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId_ProductCode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductionSchedules_CompanyId_Status_ScheduleDate",
                table: "ProductionSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ProductionCosts_CompanyId",
                table: "ProductionCosts");

            migrationBuilder.DropIndex(
                name: "IX_Materials_CompanyId_MaterialCode",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_MaterialMovements_CompanyId",
                table: "MaterialMovements");

            migrationBuilder.DropIndex(
                name: "IX_BillOfMaterials_CompanyId_ProductId_MaterialId",
                table: "BillOfMaterials");

            migrationBuilder.DropIndex(
                name: "IX_BillOfMaterials_ProductId",
                table: "BillOfMaterials");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "QualityChecks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductionSchedules");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductionCosts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialMovements");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderNo",
                table: "WorkOrders",
                column: "WorkOrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCode",
                table: "Products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialCode",
                table: "Materials",
                column: "MaterialCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterials_ProductId_MaterialId",
                table: "BillOfMaterials",
                columns: new[] { "ProductId", "MaterialId" },
                unique: true);
        }
    }
}
