using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class ConcurrentWarehouseOrderModificationException
    : Exception
{
    public ConcurrentWarehouseOrderModificationException(
        Exception? innerException = null)
        : base(
            "Zamowienie zostalo zmienione przez inna operacje. " +
            "Pobierz aktualne dane i sprobuj ponownie.",
            innerException)
    {
    }
}
