
using Microsoft.AspNetCore.Diagnostics;
using Warehouse.Application.Exceptions;

namespace Warehouse.Api.ExceptionHandling;

public sealed class BusinessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            DuplicateProductCategoryCodeException =>
                StatusCodes.Status409Conflict,

            DuplicateUnitOfMeasureCodeException =>
                StatusCodes.Status409Conflict,

            DuplicateProductCodeException =>
                StatusCodes.Status409Conflict,

            DuplicateProductBatchException =>
                StatusCodes.Status409Conflict,

            InvalidProductReferenceException =>
                StatusCodes.Status400BadRequest,

            InvalidProductBatchException =>
                StatusCodes.Status400BadRequest,

            DuplicateLogisticUnitCodeException =>
                StatusCodes.Status409Conflict,

            InvalidLogisticUnitException =>
                StatusCodes.Status400BadRequest,

            DuplicateLogisticUnitItemException =>
                StatusCodes.Status409Conflict,

            InvalidLogisticUnitItemException =>
                StatusCodes.Status400BadRequest,


            _ => 0
        };

        if (statusCode == 0)
        {
            return false;
        }

        var title = statusCode == StatusCodes.Status409Conflict
            ? "Conflict"
            : "Invalid request";

        await Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: exception.Message,
            instance: httpContext.Request.Path.Value
        ).ExecuteAsync(httpContext);

        return true;
    }
}
