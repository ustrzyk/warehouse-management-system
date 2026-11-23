
using System.Globalization;
using Moq;
using Xunit;
using Warehouse.Application.DTOs.WarehouseOrders;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class WarehouseOrderServiceTests
{
    private const int OrderId = 5;
    private const int WarehouseId = 1;
    private const int ItemId = 11;
    private const int ProductId = 100;

    private readonly Mock<IWarehouseOrderRepository> _repository = new();
    private readonly WarehouseOrderService _service;

    public WarehouseOrderServiceTests()
    {
        _service = new WarehouseOrderService(_repository.Object);
    }

    // ---------- Creating orders ----------

    [Fact]
    public async Task Create_EmptyNumber_ThrowsException()
    {
        var request = ValidCreateRequest();
        request.Number = "   ";

        await Assert.ThrowsAsync<InvalidWarehouseOrderException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_InactiveWarehouse_ThrowsException()
    {
        _repository
            .Setup(x => x.WarehouseIsActiveAsync(
                WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidWarehouseOrderException>(
            () => _service.CreateAsync(
                ValidCreateRequest(), CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_DuplicateNumber_ThrowsException()
    {
        _repository
            .Setup(x => x.WarehouseIsActiveAsync(
                WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.NumberExistsAsync(
                "ORD-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = ValidCreateRequest();
        request.Number = "  ord-001  ";

        await Assert.ThrowsAsync<DuplicateWarehouseOrderException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<WarehouseOrder>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_ValidOrder_NormalizesAndSaves()
    {
        WarehouseOrder? addedOrder = null;

        _repository
            .Setup(x => x.WarehouseIsActiveAsync(
                WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.NumberExistsAsync(
                "ORD-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.AddAsync(
                It.IsAny<WarehouseOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<WarehouseOrder, CancellationToken>((order, _) =>
            {
                order.Id = OrderId;
                order.Warehouse = TestWarehouse();
                addedOrder = order;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedOrder);

        var request = new CreateWarehouseOrderRequest
        {
            Number = "  ord-001  ",
            WarehouseId = WarehouseId,
            CustomerName = "  Test Customer  "
        };

        var result = await _service.CreateAsync(
            request, CancellationToken.None);

        Assert.Equal(OrderId, result.Id);
        Assert.Equal("ORD-001", result.Number);
        Assert.Equal("Test Customer", result.CustomerName);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(0, result.Revision);

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<WarehouseOrder>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    // ---------- Adding order items ----------

    [Fact]
    public async Task AddItem_OrderNotDraft_ThrowsException()
    {
        var order = TestOrder(WarehouseOrderStatus.Released);

        ConfigureTrackedOrder(order);

        await Assert.ThrowsAsync<InvalidWarehouseOrderException>(
            () => _service.AddItemAsync(
                OrderId,
                ValidItemRequest(),
                CancellationToken.None));

        VerifyNoSave();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.2345")]
    public async Task AddItem_InvalidQuantity_ThrowsException(
        string quantityText)
    {
        var order = TestOrder();

        ConfigureTrackedOrder(order);

        var request = ValidItemRequest();
        request.QuantityRequested = decimal.Parse(
            quantityText, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvalidWarehouseOrderException>(
            () => _service.AddItemAsync(
                OrderId, request, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_DuplicateItem_ThrowsException()
    {
        var order = TestOrder();

        ConfigureTrackedOrder(order);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.ItemExistsAsync(
                OrderId,
                ProductId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateWarehouseOrderException>(
            () => _service.AddItemAsync(
                OrderId,
                ValidItemRequest(),
                CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_Valid_IncrementsRevisionAndSaves()
    {
        var order = TestOrder(withItem: false);

        ConfigureTrackedOrder(order);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        WarehouseOrderItem? addedItem = null;

        _repository
            .Setup(x => x.AddItemAsync(
                It.IsAny<WarehouseOrderItem>(),
                It.IsAny<CancellationToken>()))
            .Callback<WarehouseOrderItem, CancellationToken>((item, _) =>
            {
                item.Id = ItemId;
                item.Product = TestProduct();
                addedItem = item;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetItemByIdAsync(
                OrderId,
                ItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedItem);

        var result = await _service.AddItemAsync(
            OrderId,
            ValidItemRequest(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ItemId, result!.Id);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal(10m, result.QuantityRequested);
        Assert.Equal(3, order.Revision);

        _repository.Verify(
            x => x.AddItemAsync(
                It.IsAny<WarehouseOrderItem>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    // ---------- Releasing orders ----------

    [Fact]
    public async Task Release_AlreadyReleased_ThrowsException()
    {
        var order = TestOrder(WarehouseOrderStatus.Released);

        ConfigureRelease(order);

        await Assert.ThrowsAsync<WarehouseOrderAlreadyReleasedException>(
            () => _service.ReleaseAsync(
                OrderId, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Release_EmptyOrder_ThrowsException()
    {
        var order = TestOrder(withItem: false);

        ConfigureRelease(order);

        await Assert.ThrowsAsync<InvalidWarehouseOrderException>(
            () => _service.ReleaseAsync(
                OrderId, CancellationToken.None));

        Assert.Equal(WarehouseOrderStatus.Draft, order.Status);

        VerifyNoSave();
    }

    [Fact]
    public async Task Release_InsufficientStock_ThrowsException()
    {
        var order = TestOrder();

        ConfigureRelease(
            order,
            new WarehouseStockBalance(ProductId, null, 4m));

        await Assert.ThrowsAsync<InsufficientWarehouseStockException>(
            () => _service.ReleaseAsync(
                OrderId, CancellationToken.None));

        Assert.Equal(WarehouseOrderStatus.Draft, order.Status);
        Assert.Null(order.ReleasedAtUtc);

        VerifyNoSave();
    }

    [Fact]
    public async Task Release_InsufficientBatchStock_ThrowsException()
    {
        var order = TestOrder(batchId: 7);

        // There is enough product overall,
        // but not enough from the requested batch.
        ConfigureRelease(
            order,
            new WarehouseStockBalance(ProductId, 7, 4m),
            new WarehouseStockBalance(ProductId, 8, 20m));

        await Assert.ThrowsAsync<InsufficientWarehouseStockException>(
            () => _service.ReleaseAsync(
                OrderId, CancellationToken.None));

        Assert.Equal(WarehouseOrderStatus.Draft, order.Status);

        VerifyNoSave();
    }

    [Fact]
    public async Task Release_SufficientStock_ChangesStatusAndSaves()
    {
        var order = TestOrder();

        ConfigureRelease(
            order,
            new WarehouseStockBalance(ProductId, null, 15m));

        var result = await _service.ReleaseAsync(
            OrderId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Released", result!.Status);
        Assert.Equal(WarehouseOrderStatus.Released, order.Status);
        Assert.NotNull(order.ReleasedAtUtc);
        Assert.Equal(3, order.Revision);

        VerifySavedOnce();
    }

    // ---------- Completing orders ----------

    [Fact]
    public async Task Complete_NotFullyPicked_ThrowsException()
    {
        var order = TestOrder(WarehouseOrderStatus.Released);

        ConfigureCompletion(
            order,
            quantityPicked: 8m,
            lastPickAt: DateTime.UtcNow);

        await Assert.ThrowsAsync<WarehouseOrderNotFullyPickedException>(
            () => _service.CompleteAsync(
                OrderId, CancellationToken.None));

        Assert.Equal(WarehouseOrderStatus.Released, order.Status);

        VerifyNoSave();
    }

    [Fact]
    public async Task Complete_MissingPickingHistory_ThrowsException()
    {
        var order = TestOrder(WarehouseOrderStatus.Released);

        ConfigureCompletion(
            order,
            quantityPicked: 10m,
            lastPickAt: null);

        await Assert.ThrowsAsync<WarehouseOrderNotFullyPickedException>(
            () => _service.CompleteAsync(
                OrderId, CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task Complete_FullyPicked_ChangesStatusAndSaves()
    {
        var order = TestOrder(WarehouseOrderStatus.Released);

        var lastPick = new DateTime(
            2026, 11, 23, 17, 0, 0, DateTimeKind.Utc);

        ConfigureCompletion(
            order,
            quantityPicked: 10m,
            lastPickAt: lastPick);

        var result = await _service.CompleteAsync(
            OrderId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        Assert.Equal(WarehouseOrderStatus.Completed, order.Status);
        Assert.Equal(lastPick, order.CompletedAtUtc);
        Assert.Equal(3, order.Revision);

        VerifySavedOnce();
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_DoesNotSaveAgain()
    {
        var order = TestOrder(WarehouseOrderStatus.Completed);

        _repository
            .Setup(x => x.GetTrackedWithItemsAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.CompleteAsync(
            OrderId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        Assert.Equal(2, order.Revision);

        _repository.Verify(
            x => x.GetItemProgressAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    // ---------- Test helpers ----------

    private void ConfigureTrackedOrder(WarehouseOrder order)
    {
        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private void ConfigureRelease(
        WarehouseOrder order,
        params WarehouseStockBalance[] balances)
    {
        _repository
            .Setup(x => x.GetTrackedWithItemsAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.BatchIsActiveForProductAsync(
                It.IsAny<int>(),
                ProductId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IReadOnlyList<WarehouseStockBalance> stock = balances;

        _repository
            .Setup(x => x.GetAvailableStockAsync(
                WarehouseId,
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);
    }

    private void ConfigureCompletion(
        WarehouseOrder order,
        decimal quantityPicked,
        DateTime? lastPickAt)
    {
        _repository
            .Setup(x => x.GetTrackedWithItemsAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        IReadOnlyList<WarehouseOrderItemProgress> progress =
        [
            new WarehouseOrderItemProgress(
                ItemId,
                10m,
                quantityPicked)
        ];

        _repository
            .Setup(x => x.GetItemProgressAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        _repository
            .Setup(x => x.GetLastPickedAtUtcAsync(
                OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastPickAt);
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

    private static CreateWarehouseOrderRequest ValidCreateRequest()
    {
        return new CreateWarehouseOrderRequest
        {
            Number = "ORD-001",
            WarehouseId = WarehouseId,
            CustomerName = "Test Customer"
        };
    }

    private static AddWarehouseOrderItemRequest ValidItemRequest()
    {
        return new AddWarehouseOrderItemRequest
        {
            ProductId = ProductId,
            QuantityRequested = 10m
        };
    }

    private static global::Warehouse.Domain.Entities.Warehouse
        TestWarehouse()
    {
        return new global::Warehouse.Domain.Entities.Warehouse
        {
            Id = WarehouseId,
            Code = "WH-01",
            Name = "Test Warehouse",
            IsActive = true
        };
    }

    private static Product TestProduct()
    {
        return new Product
        {
            Id = ProductId,
            Code = "SKU-001",
            Name = "Test Product",
            IsActive = true
        };
    }

    private static WarehouseOrder TestOrder(
        WarehouseOrderStatus status = WarehouseOrderStatus.Draft,
        bool withItem = true,
        int? batchId = null)
    {
        var order = new WarehouseOrder
        {
            Id = OrderId,
            Number = "ORD-005",
            WarehouseId = WarehouseId,
            Warehouse = TestWarehouse(),
            Status = status,
            Revision = 2
        };

        if (withItem)
        {
            order.Items.Add(new WarehouseOrderItem
            {
                Id = ItemId,
                WarehouseOrderId = OrderId,
                WarehouseOrder = order,
                ProductId = ProductId,
                Product = TestProduct(),
                ProductBatchId = batchId,
                QuantityRequested = 10m
            });
        }

        return order;
    }
}
