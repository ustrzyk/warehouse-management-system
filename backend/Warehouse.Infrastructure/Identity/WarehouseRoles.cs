
namespace Warehouse.Infrastructure.Identity;

public static class WarehouseRoles
{
    public const string Administrator = "Administrator";
    public const string WarehouseWorker = "WarehouseWorker";
    public const string Manager = "Manager";

    // Product catalog permissions
    public const string CatalogEditors =
        Administrator + "," + Manager;

    // Warehouse structure permissions
    public const string StructureEditors =
        Administrator + "," + Manager;
}
