using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class GoodsReceiptAlreadyConfirmedException : Exception
{
    public GoodsReceiptAlreadyConfirmedException()
        : base("Dokument przyjecia zostal juz zatwierdzony.")
    {
    }
}
