
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Products;

public class CreateProductRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(32)]
    public string? Ean { get; set; }

    [Range(1, int.MaxValue)]
    public int ProductCategoryId { get; set; }

    [Range(1, int.MaxValue)]
    public int UnitOfMeasureId { get; set; }
}
