using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateWarehouseCodeException : Exception
{
    public DuplicateWarehouseCodeException(
        Exception? innerException = null)
        : base(
            "Magazyn o podanym kodzie juz istnieje.",
            innerException)
    {
    }
}
