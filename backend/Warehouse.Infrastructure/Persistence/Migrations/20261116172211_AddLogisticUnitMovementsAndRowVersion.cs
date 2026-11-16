using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticUnitMovementsAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LogisticUnits",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "LogisticUnitMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LogisticUnitId = table.Column<int>(type: "int", nullable: false),
                    FromStorageLocationId = table.Column<int>(type: "int", nullable: false),
                    ToStorageLocationId = table.Column<int>(type: "int", nullable: false),
                    FromLocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToLocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticUnitMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticUnitMovements_LogisticUnits_LogisticUnitId",
                        column: x => x.LogisticUnitId,
                        principalTable: "LogisticUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogisticUnitMovements_StorageLocations_FromStorageLocationId",
                        column: x => x.FromStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogisticUnitMovements_StorageLocations_ToStorageLocationId",
                        column: x => x.ToStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitMovements_FromStorageLocationId",
                table: "LogisticUnitMovements",
                column: "FromStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitMovements_LogisticUnitId_MovedAtUtc",
                table: "LogisticUnitMovements",
                columns: new[] { "LogisticUnitId", "MovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticUnitMovements_ToStorageLocationId",
                table: "LogisticUnitMovements",
                column: "ToStorageLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogisticUnitMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LogisticUnits");
        }
    }
}
