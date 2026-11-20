
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Interfaces.Repositories;

public interface IShipmentRepository
{
    Task<WarehouseOrder?> GetOrderByIdAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<bool> ExistsForOrderAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<Shipment?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<Shipment?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<Shipment?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task AddAsync(
        Shipment shipment,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
