
namespace Warehouse.Infrastructure.Identity;

public static class WarehouseRoles
{
    public const string Administrator = "Administrator";
    public const string WarehouseWorker = "WarehouseWorker";
    public const string Manager = "Manager";

    // Roles allowed to manage the product catalog.
    public const string CatalogEditors =
        Administrator + "," + Manager;
}
