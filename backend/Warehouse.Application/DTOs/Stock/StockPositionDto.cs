
namespace Warehouse.Application.DTOs.Stock;

public record StockPositionDto(
    int WarehouseId,
    string WarehouseCode,
    string ZoneCode,
    string LocationCode,
    int LogisticUnitId,
    string LogisticUnitCode,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    int? ProductBatchId,
    string? BatchNumber,
    DateOnly? ExpirationDate,
    decimal Quantity
);
