using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class PickingConflictException : Exception
{
    public PickingConflictException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
