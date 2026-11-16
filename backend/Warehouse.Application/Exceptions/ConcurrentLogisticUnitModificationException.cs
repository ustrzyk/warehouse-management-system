
namespace Warehouse.Application.Exceptions;

public sealed class ConcurrentLogisticUnitModificationException
    : Exception
{
    public ConcurrentLogisticUnitModificationException(
        Exception? innerException = null)
        : base(
            "Paleta zostala zmieniona przez inna operacje. " +
            "Odczytaj aktualne dane i sprobuj ponownie.",
            innerException)
    {
    }
}
