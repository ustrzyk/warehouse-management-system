
namespace Warehouse.Infrastructure.Identity;

public static class WarehouseRoles
{
    public const string Administrator = "Administrator";
    public const string WarehouseWorker = "WarehouseWorker";
    public const string Manager = "Manager";

    // Product catalog
    public const string CatalogEditors =
        Administrator + "," + Manager;

    // Warehouse structure
    public const string StructureEditors =
        Administrator + "," + Manager;

    // Goods receipts, pallets and movements
    public const string LogisticsOperators =
        Administrator + "," + WarehouseWorker;

    // Warehouse order management
    public const string OrderManagers =
        Administrator + "," + Manager;
}
