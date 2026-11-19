using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class WarehouseOrderNotFullyPickedException : Exception
{
    public WarehouseOrderNotFullyPickedException(string message)
        : base(message)
    {
    }
}
