using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class WarehouseOrderAlreadyReleasedException
    : Exception
{
    public WarehouseOrderAlreadyReleasedException()
        : base("Zamowienie zostalo juz przekazane do kompletacji.")
    {
    }
}
