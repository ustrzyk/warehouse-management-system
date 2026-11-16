
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.WarehouseZones;

public class UpdateWarehouseZoneRequest
{
    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
