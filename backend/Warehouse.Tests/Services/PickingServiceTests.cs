
using Moq;
using Xunit;
using Warehouse.Application.DTOs.Picking;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class PickingServiceTests
{
    private const int OrderId = 5;
    private const int OrderItemId = 10;
    private const int PalletItemId = 20;

    private readonly Mock<IPickingRepository> _pickingRepository = new();
    private readonly Mock<IWarehouseOrderRepository> _orderRepository = new();
    private readonly PickingService _service;

    public PickingServiceTests()
    {
        _service = new PickingService(
            _pickingRepository.Object,
            _orderRepository.Object);

        _pickingRepository
            .Setup(x => x.GetByRequestIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PickingOperation?)null);
    }

    [Fact]
    public async Task Create_EmptyRequestId_ThrowsException()
    {
        var request = ValidRequest();
        request.RequestId = Guid.Empty;

        await Assert.ThrowsAsync<InvalidPickingOperationException>(
            () => _service.CreateAsync(
                OrderId, request, CancellationToken.None));

        VerifyNoSave();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Create_NonPositiveQuantity_ThrowsException(
        decimal quantity)
    {
        var request = ValidRequest(quantity);

        await Assert.ThrowsAsync<InvalidPickingOperationException>(
            () => _service.CreateAsync(
                OrderId, request, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_TooManyDecimalPlaces_ThrowsException()
    {
        var request = ValidRequest(1.2345m);

        await Assert.ThrowsAsync<InvalidPickingOperationException>(
            () => _service.CreateAsync(
                OrderId, request, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_SameRequestId_ReturnsExistingPick()
    {
        var request = ValidRequest(2m);

        var existing = new PickingOperation
        {
            Id = 77,
            RequestId = request.RequestId,
            WarehouseOrderId = OrderId,
            WarehouseOrderItemId = OrderItemId,
            LogisticUnitId = 30,
            LogisticUnitItemId = PalletItemId,
            LogisticUnitCode = "LE-030",
            ProductCode = "SKU-001",
            SourceLocationCode = "A-01",
            Quantity = 2m
        };

        _pickingRepository
            .Setup(x => x.GetByRequestIdAsync(
                request.RequestId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _service.CreateAsync(
            OrderId, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(77, result!.Id);
        Assert.Equal(2m, result.Quantity);

        _pickingRepository.Verify(
            x => x.GetTrackedOrderItemAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_ReusedRequestIdWithDifferentQuantity_Throws()
    {
        var request = ValidRequest(3m);

        var existing = new PickingOperation
        {
            Id = 77,
            RequestId = request.RequestId,
            WarehouseOrderId = OrderId,
            WarehouseOrderItemId = OrderItemId,
            LogisticUnitItemId = PalletItemId,
            Quantity = 2m
        };

        _pickingRepository
            .Setup(x => x.GetByRequestIdAsync(
                request.RequestId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await Assert.ThrowsAsync<PickingConflictException>(
            () => _service.CreateAsync(
                OrderId, request, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_OrderNotReleased_ThrowsException()
    {
        var (order, _, _) = ConfigureValidPicking();
        order.Status = WarehouseOrderStatus.Draft;

        await Assert.ThrowsAsync<InvalidPickingOperationException>(
            () => _service.CreateAsync(
                OrderId, ValidRequest(), CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_WrongProduct_ThrowsException()
    {
        var (_, _, palletItem) = ConfigureValidPicking();

        palletItem.ProductId = 999;
        palletItem.Product = new Product
        {
            Id = 999,
            Code = "WRONG-SKU",
            IsActive = true
        };

        await Assert.ThrowsAsync<InvalidPickingOperationException>(
            () => _service.CreateAsync(
                OrderId, ValidRequest(), CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_QuantityExceedsOrder_ThrowsConflict()
    {
        var (_, _, palletItem) = ConfigureValidPicking(
            requested: 10m,
            picked: 8m,
            available: 20m);

        await Assert.ThrowsAsync<PickingConflictException>(
            () => _service.CreateAsync(
                OrderId, ValidRequest(3m), CancellationToken.None));

        Assert.Equal(20m, palletItem.Quantity);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_QuantityExceedsPallet_ThrowsConflict()
    {
        var (_, _, palletItem) = ConfigureValidPicking(
            requested: 10m,
            picked: 0m,
            available: 2m);

        await Assert.ThrowsAsync<PickingConflictException>(
            () => _service.CreateAsync(
                OrderId, ValidRequest(3m), CancellationToken.None));

        Assert.Equal(2m, palletItem.Quantity);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_PartialPick_DecreasesPalletQuantity()
    {
        var (order, pallet, palletItem) = ConfigureValidPicking(
            requested: 10m,
            picked: 2m,
            available: 20m);

        ConfigureSaving();

        var request = ValidRequest(3m);

        var result = await _service.CreateAsync(
            OrderId, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3m, result!.Quantity);
        Assert.Equal(request.RequestId, result.RequestId);

        Assert.Equal(17m, palletItem.Quantity);
        Assert.Equal(WarehouseOrderStatus.Released, order.Status);
        Assert.Null(order.CompletedAtUtc);

        Assert.Equal(2, order.Revision);
        Assert.Equal(4, pallet.Revision);

        VerifySavedOnce();
    }

    [Fact]
    public async Task Create_LastPick_CompletesOrder()
    {
        var (order, pallet, palletItem) = ConfigureValidPicking(
            requested: 10m,
            picked: 8m,
            available: 5m);

        ConfigureSaving();

        var result = await _service.CreateAsync(
            OrderId, ValidRequest(2m), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2m, result!.Quantity);

        Assert.Equal(3m, palletItem.Quantity);

        Assert.Equal(
            WarehouseOrderStatus.Completed,
            order.Status);

        Assert.NotNull(order.CompletedAtUtc);
        Assert.Equal(result.PickedAtUtc, order.CompletedAtUtc);

        Assert.Equal(2, order.Revision);
        Assert.Equal(4, pallet.Revision);

        Assert.Equal("LE-030", result.LogisticUnitCode);
        Assert.Equal("SKU-001", result.ProductCode);
        Assert.Equal("A-01", result.SourceLocationCode);

        VerifySavedOnce();
    }

    private (
        WarehouseOrder Order,
        LogisticUnit Pallet,
        LogisticUnitItem PalletItem)
        ConfigureValidPicking(
            decimal requested = 10m,
            decimal picked = 0m,
            decimal available = 20m)
    {
        var warehouse = new global::Warehouse.Domain.Entities.Warehouse
        {
            Id = 1,
            Code = "WH-01",
            IsActive = true
        };

        var order = new WarehouseOrder
        {
            Id = OrderId,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            Status = WarehouseOrderStatus.Released,
            Revision = 1
        };

        var product = new Product
        {
            Id = 100,
            Code = "SKU-001",
            Name = "Test Product",
            IsActive = true
        };

        var orderItem = new WarehouseOrderItem
        {
            Id = OrderItemId,
            WarehouseOrderId = OrderId,
            WarehouseOrder = order,
            ProductId = product.Id,
            Product = product,
            QuantityRequested = requested
        };

        order.Items.Add(orderItem);

        var zone = new WarehouseZone
        {
            Id = 2,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            IsActive = true
        };

        var location = new StorageLocation
        {
            Id = 3,
            Code = "A-01",
            WarehouseZoneId = zone.Id,
            WarehouseZone = zone,
            IsActive = true
        };

        var pallet = new LogisticUnit
        {
            Id = 30,
            Code = "LE-030",
            StorageLocationId = location.Id,
            StorageLocation = location,
            Status = LogisticUnitStatus.Stored,
            Revision = 3
        };

        var palletItem = new LogisticUnitItem
        {
            Id = PalletItemId,
            LogisticUnitId = pallet.Id,
            LogisticUnit = pallet,
            ProductId = product.Id,
            Product = product,
            Quantity = available
        };

        pallet.Items.Add(palletItem);

        _pickingRepository
            .Setup(x => x.GetTrackedOrderItemAsync(
                OrderId,
                OrderItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItem);

        _pickingRepository
            .Setup(x => x.GetTrackedPalletItemAsync(
                PalletItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(palletItem);

        IReadOnlyList<WarehouseOrderItemProgress> progress =
        [
            new WarehouseOrderItemProgress(
                OrderItemId,
                requested,
                picked)
        ];

        _orderRepository
            .Setup(x => x.GetItemProgressAsync(
                OrderId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        return (order, pallet, palletItem);
    }

    private void ConfigureSaving()
    {
        PickingOperation? savedOperation = null;

        _pickingRepository
            .Setup(x => x.AddAsync(
                It.IsAny<PickingOperation>(),
                It.IsAny<CancellationToken>()))
            .Callback<PickingOperation, CancellationToken>(
                (operation, _) =>
                {
                    operation.Id = 77;
                    savedOperation = operation;
                })
            .Returns(Task.CompletedTask);

        _pickingRepository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pickingRepository
            .Setup(x => x.GetByIdAsync(
                OrderId,
                77,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedOperation);
    }

    private void VerifyNoSave()
    {
        _pickingRepository.Verify(
            x => x.AddAsync(
                It.IsAny<PickingOperation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _pickingRepository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifySavedOnce()
    {
        _pickingRepository.Verify(
            x => x.AddAsync(
                It.IsAny<PickingOperation>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _pickingRepository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CreatePickingRequest ValidRequest(
        decimal quantity = 2m)
    {
        return new CreatePickingRequest
        {
            RequestId = Guid.NewGuid(),
            WarehouseOrderItemId = OrderItemId,
            LogisticUnitItemId = PalletItemId,
            Quantity = quantity
        };
    }
}
