using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidWarehouseException : Exception
{
    public InvalidWarehouseException(string message)
        : base(message)
    {
    }
}
