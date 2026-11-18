using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidPickingOperationException : Exception
{
    public InvalidPickingOperationException(string message)
        : base(message)
    {
    }
}
