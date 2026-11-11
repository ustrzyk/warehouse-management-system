using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.UnitsOfMeasure;

public class CreateUnitOfMeasureRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Symbol { get; set; } = string.Empty;
}