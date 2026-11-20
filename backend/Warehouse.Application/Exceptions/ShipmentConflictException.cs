using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class ShipmentConflictException : Exception
{
    public ShipmentConflictException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
