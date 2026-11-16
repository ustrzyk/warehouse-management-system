
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.StorageLocations;

public class CreateStorageLocationRequest
{
    [Range(1, int.MaxValue)]
    public int WarehouseZoneId { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }
}
