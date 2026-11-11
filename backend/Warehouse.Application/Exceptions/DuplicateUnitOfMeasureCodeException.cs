using System;
using System.Collections.Generic;
using System.Text;

namespace Warehouse.Application.Exceptions;

public sealed class DuplicateUnitOfMeasureCodeException
    : Exception
{
    public DuplicateUnitOfMeasureCodeException(
        Exception? innerException = null)
        : base(
            "Jednostka miary o podanym kodzie już istnieje.",
            innerException)
    {
    }
}