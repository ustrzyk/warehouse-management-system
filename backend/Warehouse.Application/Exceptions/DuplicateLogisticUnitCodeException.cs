using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateLogisticUnitCodeException : Exception
{
    public DuplicateLogisticUnitCodeException(
        Exception? innerException = null)
        : base("Paleta o podanym numerze LE już istnieje.",
            innerException)
    {
    }
}
