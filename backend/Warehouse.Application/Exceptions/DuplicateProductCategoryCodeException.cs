using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateProductCategoryCodeException
    : Exception
{
    public DuplicateProductCategoryCodeException(
        Exception? innerException = null)
        : base(
            "Kategoria o podanym kodzie już istnieje.",
            innerException)
    {
    }
}
