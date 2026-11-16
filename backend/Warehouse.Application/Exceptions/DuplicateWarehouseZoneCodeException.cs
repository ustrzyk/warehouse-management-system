using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateWarehouseZoneCodeException : Exception
{
    public DuplicateWarehouseZoneCodeException(
        Exception? innerException = null)
        : base(
            "Strefa o podanym kodzie juz istnieje w tym magazynie.",
            innerException)
    {
    }
}
