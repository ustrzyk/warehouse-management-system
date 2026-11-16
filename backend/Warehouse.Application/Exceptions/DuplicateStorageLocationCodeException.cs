using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateStorageLocationCodeException : Exception
{
    public DuplicateStorageLocationCodeException(
        Exception? innerException = null)
        : base(
            "Lokalizacja o podanym kodzie juz istnieje w tej strefie.",
            innerException)
    {
    }
}
