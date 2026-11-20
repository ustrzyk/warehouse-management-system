
using Warehouse.Application.DTOs.Shipments;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Services;

public class ShipmentService : IShipmentService
{
    private readonly IShipmentRepository _repository;

    public ShipmentService(IShipmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShipmentDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var shipment = await _repository.GetByIdAsync(
            id, cancellationToken);

        return shipment is null ? null : MapToDto(shipment);
    }

    public async Task<ShipmentDto?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var shipment = await _repository.GetByOrderIdAsync(
            orderId, cancellationToken);

        return shipment is null ? null : MapToDto(shipment);
    }

    public async Task<ShipmentDto> CreateAsync(
        CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CarrierName))
        {
            throw new InvalidShipmentException(
                "Przewoznik nie moze byc pusty.");
        }

        var order = await _repository.GetOrderByIdAsync(
            request.WarehouseOrderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidShipmentException(
                "Zamowienie nie istnieje.");
        }

        if (order.Status != WarehouseOrderStatus.Completed)
        {
            throw new InvalidShipmentException(
                "Wysylke mozna utworzyc tylko dla zamowienia Completed.");
        }

        if (await _repository.ExistsForOrderAsync(
            order.Id, cancellationToken))
        {
            throw new ShipmentConflictException(
                "Wysylka dla tego zamowienia juz istnieje.");
        }

        var shipment = new Shipment
        {
            Number = $"SHP-{order.Id:D8}",
            WarehouseOrderId = order.Id,

            CarrierName = request.CarrierName.Trim(),
            TrackingNumber = string.IsNullOrWhiteSpace(
                request.TrackingNumber)
                ? null
                : request.TrackingNumber.Trim(),

            Status = ShipmentStatus.Prepared,
            CreatedAtUtc = DateTime.UtcNow,
            Revision = 0
        };

        await _repository.AddAsync(
            shipment, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        var created = await _repository.GetByIdAsync(
            shipment.Id, cancellationToken);

        return created is null
            ? throw new InvalidOperationException(
                "Created shipment could not be loaded.")
            : MapToDto(created);
    }

    public async Task<ShipmentDto?> DispatchAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var shipment = await _repository.GetTrackedByIdAsync(
            id, cancellationToken);

        if (shipment is null)
            return null;

        if (shipment.Status != ShipmentStatus.Prepared)
        {
            throw new ShipmentConflictException(
                "Wysylka zostala juz potwierdzona.");
        }

        var order = shipment.WarehouseOrder;

        if (order.Status != WarehouseOrderStatus.Completed)
        {
            throw new ShipmentConflictException(
                "Zamowienie nie ma statusu Completed.");
        }

        var now = DateTime.UtcNow;

        // Potwierdzenie wysylki.
        shipment.Status = ShipmentStatus.Shipped;
        shipment.ShippedAtUtc = now;
        shipment.Revision++;

        // Aktualizacja dokumentu zamowienia.
        order.Status = WarehouseOrderStatus.Shipped;
        order.ShippedAtUtc = now;
        order.Revision++;

        // Jeden atomowy zapis.
        // Nie zmieniamy ilosci na paletach.
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToDto(shipment);
    }

    private static ShipmentDto MapToDto(Shipment shipment)
    {
        return new ShipmentDto(
            shipment.Id,
            shipment.Number,
            shipment.WarehouseOrderId,
            shipment.WarehouseOrder.Number,
            shipment.CarrierName,
            shipment.TrackingNumber,
            shipment.Status.ToString(),
            shipment.CreatedAtUtc,
            shipment.ShippedAtUtc,
            shipment.Revision);
    }
}
