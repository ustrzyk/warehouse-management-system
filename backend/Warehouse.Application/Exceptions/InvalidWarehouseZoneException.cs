using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidWarehouseZoneException : Exception
{
    public InvalidWarehouseZoneException(string message)
        : base(message)
    {
    }
}
