
using Warehouse.Application.DTOs.Shipments;

namespace Warehouse.Application.Interfaces.Services;

public interface IShipmentService
{
    Task<ShipmentDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ShipmentDto?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<ShipmentDto> CreateAsync(
        CreateShipmentRequest request,
        CancellationToken cancellationToken);

    Task<ShipmentDto?> DispatchAsync(
        int id,
        CancellationToken cancellationToken);
}
