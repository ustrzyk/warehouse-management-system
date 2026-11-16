using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.Exceptions;

public sealed class InvalidGoodsReceiptException : Exception
{
    public InvalidGoodsReceiptException(string message)
        : base(message)
    {
    }
}
