using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class ConcurrentShipmentModificationException : Exception
{
    public ConcurrentShipmentModificationException(Exception innerException)
        : base(
            "Wysylka lub zamowienie zostaly zmienione przez inna operacje.",
            innerException)
    {
    }
}
