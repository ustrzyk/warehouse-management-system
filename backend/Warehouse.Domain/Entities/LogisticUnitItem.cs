using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Entities;

public class LogisticUnitItem
{
    public int Id { get; set; }

    // Paleta, do ktorej nalezy pozycja
    public int LogisticUnitId { get; set; }

    public LogisticUnit LogisticUnit { get; set; } = null!;

    // Artykul
    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    // Opcjonalna partia artykulu
    public int? ProductBatchId { get; set; }

    public ProductBatch? ProductBatch { get; set; }

    // Ilosc w podstawowej jednostce produktu
    public decimal Quantity { get; set; }
}
