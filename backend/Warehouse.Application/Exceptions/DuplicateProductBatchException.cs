using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateProductBatchException : Exception
{
    public DuplicateProductBatchException(
        Exception? innerException = null)
        : base(
            "Partia o podanym numerze istnieje już dla tego produktu.",
            innerException)
    {
    }
}
