using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class WarehouseOrderItem
{
    public int Id { get; set; }

    public int WarehouseOrderId { get; set; }
    public WarehouseOrder WarehouseOrder { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    // Ilosc zamowiona, nie pobrana.
    public decimal QuantityRequested { get; set; }
}
