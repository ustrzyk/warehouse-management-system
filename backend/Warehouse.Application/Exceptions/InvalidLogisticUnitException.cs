using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidLogisticUnitException : Exception
{
    public InvalidLogisticUnitException(string message)
        : base(message)
    {
    }
}
