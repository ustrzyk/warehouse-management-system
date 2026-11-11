using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateProductCodeException : Exception
{
    public DuplicateProductCodeException(
        Exception? innerException = null)
        : base(
            "Produkt o podanym kodzie już istnieje.",
            innerException)
    {
    }
}
