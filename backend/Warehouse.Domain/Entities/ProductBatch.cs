using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class ProductBatch
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string BatchNumber { get; set; } = string.Empty;

    public DateOnly? ExpirationDate { get; set; }

    public DateOnly? ProductionDate { get; set; }

    public bool IsActive { get; set; } = true;
}
