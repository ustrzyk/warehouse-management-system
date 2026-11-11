
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.ProductCategories;

public class UpdateProductCategoryRequest
{
    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
