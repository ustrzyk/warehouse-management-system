
using Moq;
using Xunit;
using Warehouse.Application.DTOs.Shipments;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class ShipmentServiceTests
{
    private const int OrderId = 5;
    private const int ShipmentId = 20;

    private readonly Mock<IShipmentRepository> _repository = new();
    private readonly ShipmentService _service;

    public ShipmentServiceTests()
    {
        _service = new ShipmentService(_repository.Object);
    }

    // ---------- Reading shipments ----------

    [Fact]
    public async Task GetById_MissingShipment_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await _service.GetByIdAsync(
            ShipmentId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task GetById_ExistingShipment_ReturnsDto()
    {
        var shipment = TestShipment();

        _repository
            .Setup(x => x.GetByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var result = await _service.GetByIdAsync(
            ShipmentId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ShipmentId, result!.Id);
        Assert.Equal("SHP-00000005", result.Number);
        Assert.Equal("ORD-005", result.WarehouseOrderNumber);
        Assert.Equal("Prepared", result.Status);

        VerifyNoSave();
    }

    [Fact]
    public async Task GetByOrderId_MissingShipment_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetByOrderIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await _service.GetByOrderIdAsync(
            OrderId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task GetByOrderId_ExistingShipment_ReturnsDto()
    {
        var shipment = TestShipment();

        _repository
            .Setup(x => x.GetByOrderIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var result = await _service.GetByOrderIdAsync(
            OrderId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ShipmentId, result!.Id);
        Assert.Equal(OrderId, result.WarehouseOrderId);
        Assert.Equal("DHL", result.CarrierName);

        VerifyNoSave();
    }

    // ---------- Creating shipments ----------

    [Fact]
    public async Task Create_EmptyCarrier_ThrowsException()
    {
        var request = ValidRequest();
        request.CarrierName = "   ";

        await Assert.ThrowsAsync<InvalidShipmentException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.GetOrderByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_MissingOrder_ThrowsException()
    {
        _repository
            .Setup(x => x.GetOrderByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WarehouseOrder?)null);

        await Assert.ThrowsAsync<InvalidShipmentException>(
            () => _service.CreateAsync(
                ValidRequest(), CancellationToken.None));

        VerifyNoSave();
    }

    [Theory]
    [InlineData(WarehouseOrderStatus.Draft)]
    [InlineData(WarehouseOrderStatus.Released)]
    [InlineData(WarehouseOrderStatus.Shipped)]
    public async Task Create_OrderNotCompleted_ThrowsException(
        WarehouseOrderStatus status)
    {
        var order = TestOrder(status);

        _repository
            .Setup(x => x.GetOrderByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidShipmentException>(
            () => _service.CreateAsync(
                ValidRequest(), CancellationToken.None));

        _repository.Verify(
            x => x.ExistsForOrderAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_ExistingShipment_ThrowsConflict()
    {
        var order = TestOrder();

        _repository
            .Setup(x => x.GetOrderByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repository
            .Setup(x => x.ExistsForOrderAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ShipmentConflictException>(
            () => _service.CreateAsync(
                ValidRequest(), CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<Shipment>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_Valid_NormalizesFieldsAndSaves()
    {
        ConfigureValidCreation();

        var request = new CreateShipmentRequest
        {
            WarehouseOrderId = OrderId,
            CarrierName = "  DHL  ",
            TrackingNumber = "  TRACK-123  "
        };

        var result = await _service.CreateAsync(
            request, CancellationToken.None);

        Assert.Equal(ShipmentId, result.Id);
        Assert.Equal("SHP-00000005", result.Number);
        Assert.Equal(OrderId, result.WarehouseOrderId);
        Assert.Equal("ORD-005", result.WarehouseOrderNumber);
        Assert.Equal("DHL", result.CarrierName);
        Assert.Equal("TRACK-123", result.TrackingNumber);
        Assert.Equal("Prepared", result.Status);
        Assert.Equal(0, result.Revision);
        Assert.Null(result.ShippedAtUtc);

        _repository.Verify(
            x => x.AddAsync(
                It.Is<Shipment>(s =>
                    s.Number == "SHP-00000005" &&
                    s.WarehouseOrderId == OrderId &&
                    s.CarrierName == "DHL" &&
                    s.TrackingNumber == "TRACK-123" &&
                    s.Status == ShipmentStatus.Prepared),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    [Fact]
    public async Task Create_EmptyTrackingNumber_BecomesNull()
    {
        ConfigureValidCreation();

        var request = ValidRequest();
        request.TrackingNumber = "   ";

        var result = await _service.CreateAsync(
            request, CancellationToken.None);

        Assert.Null(result.TrackingNumber);
        Assert.Equal("Prepared", result.Status);

        VerifySavedOnce();
    }

    // ---------- Dispatching shipments ----------

    [Fact]
    public async Task Dispatch_MissingShipment_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await _service.DispatchAsync(
            ShipmentId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task Dispatch_AlreadyShipped_ThrowsConflict()
    {
        var shipment = TestShipment(
            shipmentStatus: ShipmentStatus.Shipped);

        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        await Assert.ThrowsAsync<ShipmentConflictException>(
            () => _service.DispatchAsync(
                ShipmentId, CancellationToken.None));

        Assert.Equal(2, shipment.Revision);
        VerifyNoSave();
    }

    [Fact]
    public async Task Dispatch_OrderNotCompleted_ThrowsConflict()
    {
        var shipment = TestShipment(
            orderStatus: WarehouseOrderStatus.Released);

        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        await Assert.ThrowsAsync<ShipmentConflictException>(
            () => _service.DispatchAsync(
                ShipmentId, CancellationToken.None));

        Assert.Equal(ShipmentStatus.Prepared, shipment.Status);
        Assert.Null(shipment.ShippedAtUtc);
        Assert.Equal(2, shipment.Revision);

        VerifyNoSave();
    }

    [Fact]
    public async Task Dispatch_Valid_UpdatesShipmentAndOrder()
    {
        var shipment = TestShipment();
        var order = shipment.WarehouseOrder;

        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        _repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;

        var result = await _service.DispatchAsync(
            ShipmentId, CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.Equal("Shipped", result!.Status);
        Assert.Equal(ShipmentStatus.Shipped, shipment.Status);
        Assert.Equal(WarehouseOrderStatus.Shipped, order.Status);

        Assert.NotNull(shipment.ShippedAtUtc);
        Assert.NotNull(order.ShippedAtUtc);

        Assert.Equal(
            shipment.ShippedAtUtc,
            order.ShippedAtUtc);

        Assert.Equal(
            shipment.ShippedAtUtc,
            result.ShippedAtUtc);

        Assert.InRange(
            shipment.ShippedAtUtc!.Value,
            before,
            after);

        Assert.Equal(3, shipment.Revision);
        Assert.Equal(4, order.Revision);
        Assert.Equal(3, result.Revision);

        VerifySavedOnce();
    }

    // ---------- Test helpers ----------

    private void ConfigureValidCreation()
    {
        var order = TestOrder();
        Shipment? createdShipment = null;

        _repository
            .Setup(x => x.GetOrderByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repository
            .Setup(x => x.ExistsForOrderAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.AddAsync(
                It.IsAny<Shipment>(),
                It.IsAny<CancellationToken>()))
            .Callback<Shipment, CancellationToken>((shipment, _) =>
            {
                shipment.Id = ShipmentId;
                shipment.WarehouseOrder = order;
                createdShipment = shipment;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetByIdAsync(
                ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => createdShipment);
    }

    private void VerifyNoSave()
    {
        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifySavedOnce()
    {
        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CreateShipmentRequest ValidRequest()
    {
        return new CreateShipmentRequest
        {
            WarehouseOrderId = OrderId,
            CarrierName = "DHL",
            TrackingNumber = "TRACK-001"
        };
    }

    private static WarehouseOrder TestOrder(
        WarehouseOrderStatus status = WarehouseOrderStatus.Completed)
    {
        return new WarehouseOrder
        {
            Id = OrderId,
            Number = "ORD-005",
            Status = status,
            Revision = 3
        };
    }

    private static Shipment TestShipment(
        WarehouseOrderStatus orderStatus =
            WarehouseOrderStatus.Completed,
        ShipmentStatus shipmentStatus =
            ShipmentStatus.Prepared)
    {
        return new Shipment
        {
            Id = ShipmentId,
            Number = "SHP-00000005",
            WarehouseOrderId = OrderId,
            WarehouseOrder = TestOrder(orderStatus),
            CarrierName = "DHL",
            TrackingNumber = "TRACK-001",
            Status = shipmentStatus,
            Revision = 2
        };
    }
}
