using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidProductBatchException : Exception
{
    public InvalidProductBatchException(string message)
        : base(message)
    {
    }
}
