using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class ConcurrentGoodsReceiptModificationException
    : Exception
{
    public ConcurrentGoodsReceiptModificationException(
        Exception? innerException = null)
        : base(
            "Dokument zostal zmieniony przez inna operacje. " +
            "Pobierz aktualne dane i sprobuj ponownie.",
            innerException)
    {
    }
}
