using System;
using System.Collections.Generic;
using System.Text;


using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.ProductBatches;

public class CreateProductBatchRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string BatchNumber { get; set; } = string.Empty;

    public DateOnly? ProductionDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }
}
