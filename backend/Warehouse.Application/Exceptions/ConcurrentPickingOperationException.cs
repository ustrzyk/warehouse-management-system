using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class ConcurrentPickingOperationException : Exception
{
    public ConcurrentPickingOperationException(
        Exception innerException)
        : base(
            "Dane zamowienia lub palety zmienily sie podczas " +
            "kompletacji. Odczytaj je ponownie i powtorz operacje.",
            innerException)
    {
    }
}
