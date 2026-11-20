
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Repositories;

public class ShipmentRepository : IShipmentRepository
{
    private readonly WarehouseDbContext _context;

    public ShipmentRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public Task<WarehouseOrder?> GetOrderByIdAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return _context.WarehouseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken);
    }

    public Task<bool> ExistsForOrderAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return _context.Shipments
            .AsNoTracking()
            .AnyAsync(
                x => x.WarehouseOrderId == orderId,
                cancellationToken);
    }

    public Task<Shipment?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Shipments
            .AsNoTracking()
            .Include(x => x.WarehouseOrder)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<Shipment?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        return _context.Shipments
            .AsNoTracking()
            .Include(x => x.WarehouseOrder)
            .FirstOrDefaultAsync(
                x => x.WarehouseOrderId == orderId,
                cancellationToken);
    }

    public Task<Shipment?> GetTrackedByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Shipments
            .Include(x => x.WarehouseOrder)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        await _context.Shipments.AddAsync(
            shipment, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrentShipmentModificationException(ex);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException
            { Number: 2601 or 2627 })
        {
            throw new ShipmentConflictException(
                "Wysylka dla tego zamowienia juz istnieje " +
                "lub jej numer jest zajety.",
                ex);
        }
    }
}
