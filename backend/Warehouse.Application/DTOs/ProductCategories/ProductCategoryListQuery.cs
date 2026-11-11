
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.ProductCategories;

public class ProductCategoryListQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    [Required]
    [RegularExpression("(?i)^(name|code)$")]
    public string SortBy { get; set; } = "name";

    public bool Descending { get; set; } = false;

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
