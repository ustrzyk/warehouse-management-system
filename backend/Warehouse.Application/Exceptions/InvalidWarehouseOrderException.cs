using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidWarehouseOrderException : Exception
{
    public InvalidWarehouseOrderException(string message)
        : base(message)
    {
    }
}
