using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class GoodsReceiptItem
{
    public int Id { get; set; }

    public int GoodsReceiptId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public decimal Quantity { get; set; }
}
