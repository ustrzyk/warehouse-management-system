using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidStorageLocationException : Exception
{
    public InvalidStorageLocationException(string message)
        : base(message)
    {
    }
}
