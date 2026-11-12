using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProductBatches_Id_ProductId",
                table: "ProductBatches",
                columns: new[] { "Id", "ProductId" });

            migrationBuilder.CreateTable(
                name: "LogisticUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticUnits_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogisticUnitItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LogisticUnitId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductBatchId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticUnitItems", x => x.Id);
                    table.CheckConstraint("CK_LogisticUnitItems_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_LogisticUnitItems_LogisticUnits_LogisticUnitId",
                        column: x => x.LogisticUnitId,
                        principalTable: "LogisticUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogisticUnitItems_ProductBatches_ProductBatchId_ProductId",
                        columns: x => new { x.ProductBatchId, x.ProductId },
                        principalTable: "ProductBatches",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogisticUnitItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitItems_LogisticUnitId_ProductId_ProductBatchId",
                table: "LogisticUnitItems",
                columns: new[] { "LogisticUnitId", "ProductId", "ProductBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitItems_ProductBatchId_ProductId",
                table: "LogisticUnitItems",
                columns: new[] { "ProductBatchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitItems_ProductId",
                table: "LogisticUnitItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnits_Code",
                table: "LogisticUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnits_StorageLocationId",
                table: "LogisticUnits",
                column: "StorageLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogisticUnitItems");

            migrationBuilder.DropTable(
                name: "LogisticUnits");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProductBatches_Id_ProductId",
                table: "ProductBatches");
        }
    }
}
