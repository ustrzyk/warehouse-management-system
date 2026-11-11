using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidProductReferenceException : Exception
{
    public InvalidProductReferenceException(string message)
        : base(message)
    {
    }
}
