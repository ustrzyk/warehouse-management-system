using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class DuplicateGoodsReceiptException : Exception
{
    public DuplicateGoodsReceiptException(
        Exception? innerException = null)
        : base(
            "Dokument o tym numerze lub taka pozycja juz istnieje.",
            innerException)
    {
    }
}
