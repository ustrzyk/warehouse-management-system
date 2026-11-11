using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class UnitOfMeasure
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; }
        = new List<Product>();
}
