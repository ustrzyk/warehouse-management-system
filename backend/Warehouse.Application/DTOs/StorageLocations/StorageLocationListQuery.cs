
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.StorageLocations;

public class StorageLocationListQuery
{
    [Range(1, int.MaxValue)]
    public int? WarehouseId { get; set; }

    [Range(1, int.MaxValue)]
    public int? WarehouseZoneId { get; set; }

    [StringLength(100)]
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
