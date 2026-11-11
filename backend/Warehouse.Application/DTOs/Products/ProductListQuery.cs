
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Products;

public class ProductListQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int? ProductCategoryId { get; set; }

    [Range(1, int.MaxValue)]
    public int? UnitOfMeasureId { get; set; }

    [Required]
    [RegularExpression("(?i)^(code|name|ean)$")]
    public string SortBy { get; set; } = "name";

    public bool Descending { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
