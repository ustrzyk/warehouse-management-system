using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateWarehouseOrderException : Exception
{
    public DuplicateWarehouseOrderException(
        Exception? innerException = null)
        : base(
            "Zamowienie o podanym numerze lub taka pozycja juz istnieje.",
            innerException)
    {
    }
}
