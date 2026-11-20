using System;
using System.Collections.Generic;
using System.Text;



namespace Warehouse.Domain.Enums;

public enum WarehouseOrderStatus
{
    Draft = 0,
    Released = 1,
    Completed = 2,
    Shipped = 3
}
