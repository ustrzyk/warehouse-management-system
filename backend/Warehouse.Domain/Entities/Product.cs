using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Ean { get; set; }

    public int ProductCategoryId { get; set; }

    public ProductCategory ProductCategory { get; set; } = null!;

    public int UnitOfMeasureId { get; set; }

    public UnitOfMeasure UnitOfMeasure { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public ICollection<ProductBatch> Batches { get; set; }
        = new List<ProductBatch>();
}
