using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InsufficientWarehouseStockException
    : Exception
{
    public InsufficientWarehouseStockException(string message)
        : base(message)
    {
    }
}
