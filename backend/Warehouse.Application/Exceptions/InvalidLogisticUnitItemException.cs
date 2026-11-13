using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidLogisticUnitItemException : Exception
{
    public InvalidLogisticUnitItemException(string message)
        : base(message)
    {
    }
}
