
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.ProductBatches;

public class UpdateProductBatchRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string BatchNumber { get; set; } = string.Empty;

    public DateOnly? ProductionDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public bool IsActive { get; set; } = true;
}
