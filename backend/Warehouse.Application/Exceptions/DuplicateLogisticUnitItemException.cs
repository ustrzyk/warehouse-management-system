using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateLogisticUnitItemException : Exception
{
    public DuplicateLogisticUnitItemException(
        Exception? innerException = null)
        : base(
            "Ten produkt i partia znajduja sie juz na palecie.",
            innerException)
    {
    }
}
