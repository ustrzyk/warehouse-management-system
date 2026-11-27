
namespace Warehouse.Application.DTOs.Stock;

public record StockAvailabilityDto(
    int WarehouseId,
    string WarehouseCode,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal PhysicalQuantity,
    decimal OutstandingQuantity,
    decimal FreeQuantity
);
