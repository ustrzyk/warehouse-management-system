using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Domain.Enums;

public enum LogisticUnitStatus
{
    Created = 0,
    Stored = 1,
    Picking = 2,
    Shipped = 3
}
