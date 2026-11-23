
using System.Globalization;
using Moq;
using Xunit;
using Warehouse.Application.DTOs.LogisticUnitItems;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class LogisticUnitItemServiceTests
{
    private const int UnitId = 10;
    private const int ItemId = 20;
    private const int ProductId = 30;
    private const int BatchId = 40;

    private readonly Mock<ILogisticUnitItemRepository> _repository = new();
    private readonly LogisticUnitItemService _service;

    private LogisticUnitItem? _createdItem;

    public LogisticUnitItemServiceTests()
    {
        _service = new LogisticUnitItemService(_repository.Object);
    }

    // ---------- Reading pallet items ----------

    [Fact]
    public async Task GetItems_MissingPallet_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetLogisticUnitAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.GetItemsAsync(
            UnitId, CancellationToken.None);

        Assert.Null(result);

        _repository.Verify(
            x => x.GetItemsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task GetItems_EmptyPallet_ReturnsEmptyList()
    {
        ConfigurePallet();

        _repository
            .Setup(x => x.GetItemsAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LogisticUnitItem>());

        var result = await _service.GetItemsAsync(
            UnitId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!);

        VerifyNoSave();
    }

    [Fact]
    public async Task GetItems_ExistingPallet_MapsProductsAndBatches()
    {
        ConfigurePallet();

        var items = new List<LogisticUnitItem>
        {
            TestItem(ItemId, BatchId, 12.500m),
            TestItem(ItemId + 1, null, 3m)
        };

        _repository
            .Setup(x => x.GetItemsAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _service.GetItemsAsync(
            UnitId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);

        var first = result[0];

        Assert.Equal(ItemId, first.Id);
        Assert.Equal(UnitId, first.LogisticUnitId);
        Assert.Equal(ProductId, first.ProductId);
        Assert.Equal("ART-030", first.ProductCode);
        Assert.Equal("Test Product", first.ProductName);
        Assert.Equal(BatchId, first.ProductBatchId);
        Assert.Equal("BATCH-040", first.BatchNumber);
        Assert.Equal(12.500m, first.Quantity);

        var second = result[1];

        Assert.Equal(ItemId + 1, second.Id);
        Assert.Null(second.ProductBatchId);
        Assert.Null(second.BatchNumber);
        Assert.Equal(3m, second.Quantity);

        VerifyNoSave();
    }

    [Fact]
    public async Task GetById_MissingItem_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetByIdAsync(
                UnitId,
                ItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnitItem?)null);

        var result = await _service.GetByIdAsync(
            UnitId, ItemId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task GetById_ExistingItem_ReturnsDto()
    {
        var item = TestItem();

        _repository
            .Setup(x => x.GetByIdAsync(
                UnitId,
                ItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _service.GetByIdAsync(
            UnitId, ItemId, CancellationToken.None);

        Assert.NotNull(result);

        Assert.Equal(ItemId, result!.Id);
        Assert.Equal(UnitId, result.LogisticUnitId);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal("ART-030", result.ProductCode);
        Assert.Equal("Test Product", result.ProductName);
        Assert.Equal(BatchId, result.ProductBatchId);
        Assert.Equal("BATCH-040", result.BatchNumber);
        Assert.Equal(10.500m, result.Quantity);

        VerifyNoSave();
    }

    // ---------- Creating pallet items ----------

    [Fact]
    public async Task Create_MissingPallet_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetLogisticUnitAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.CreateAsync(
            UnitId,
            ValidRequest(),
            CancellationToken.None);

        Assert.Null(result);

        _repository.Verify(
            x => x.IsGeneratedByGoodsReceiptAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Theory]
    [InlineData(LogisticUnitStatus.Stored)]
    [InlineData(LogisticUnitStatus.Picking)]
    [InlineData(LogisticUnitStatus.Shipped)]
    public async Task Create_PalletNotCreated_ThrowsException(
        LogisticUnitStatus status)
    {
        ConfigurePallet(status);

        await Assert.ThrowsAsync<InvalidLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                ValidRequest(),
                CancellationToken.None));

        _repository.Verify(
            x => x.IsGeneratedByGoodsReceiptAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_GoodsReceiptPallet_ThrowsException()
    {
        ConfigurePallet();

        _repository
            .Setup(x => x.IsGeneratedByGoodsReceiptAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                ValidRequest(),
                CancellationToken.None));

        _repository.Verify(
            x => x.ProductIsActiveAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    // Quantity must be positive, within range,
    // and contain at most 3 decimal places.

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("1000000000000000")]
    [InlineData("1.0001")]
    public async Task Create_InvalidQuantity_ThrowsException(
        string quantityText)
    {
        ConfigurePallet();

        var quantity = decimal.Parse(
            quantityText,
            CultureInfo.InvariantCulture);

        var request = ValidRequest();
        request.Quantity = quantity;

        await Assert.ThrowsAsync<InvalidLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                request,
                CancellationToken.None));

        _repository.Verify(
            x => x.ProductIsActiveAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_InactiveProduct_ThrowsException()
    {
        ConfigurePallet();

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                ValidRequest(),
                CancellationToken.None));

        _repository.Verify(
            x => x.BatchIsActiveForProductAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_InvalidProductBatch_ThrowsException()
    {
        ConfigurePallet();

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.BatchIsActiveForProductAsync(
                BatchId,
                ProductId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                ValidRequest(),
                CancellationToken.None));

        _repository.Verify(
            x => x.ItemExistsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BatchId)]
    public async Task Create_DuplicateItem_ThrowsException(
        int? batchId)
    {
        ConfigureValidCreation();

        _repository
            .Setup(x => x.ItemExistsAsync(
                UnitId,
                ProductId,
                batchId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = ValidRequest(batchId: batchId);

        await Assert.ThrowsAsync<DuplicateLogisticUnitItemException>(
            () => _service.CreateAsync(
                UnitId,
                request,
                CancellationToken.None));

        VerifyNoAdd();
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_ValidWithBatch_SavesAndMapsItem()
    {
        ConfigureValidCreation();

        var request = ValidRequest(
            quantity: 12.345m,
            batchId: BatchId);

        var result = await _service.CreateAsync(
            UnitId,
            request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(_createdItem);

        Assert.Equal(ItemId, result!.Id);
        Assert.Equal(UnitId, result.LogisticUnitId);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal("ART-030", result.ProductCode);
        Assert.Equal("Test Product", result.ProductName);
        Assert.Equal(BatchId, result.ProductBatchId);
        Assert.Equal("BATCH-040", result.BatchNumber);
        Assert.Equal(12.345m, result.Quantity);

        Assert.Equal(UnitId, _createdItem!.LogisticUnitId);
        Assert.Equal(ProductId, _createdItem.ProductId);
        Assert.Equal(BatchId, _createdItem.ProductBatchId);
        Assert.Equal(12.345m, _createdItem.Quantity);

        _repository.Verify(
            x => x.BatchIsActiveForProductAsync(
                BatchId,
                ProductId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repository.Verify(
            x => x.ItemExistsAsync(
                UnitId,
                ProductId,
                BatchId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyAddedOnce();
        VerifySavedOnce();
    }

    [Fact]
    public async Task Create_ValidWithoutBatch_SavesAndMapsItem()
    {
        ConfigureValidCreation();

        var request = ValidRequest(
            quantity: 5m,
            batchId: null);

        var result = await _service.CreateAsync(
            UnitId,
            request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(_createdItem);

        Assert.Equal(ItemId, result!.Id);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Null(result.ProductBatchId);
        Assert.Null(result.BatchNumber);
        Assert.Equal(5m, result.Quantity);

        Assert.Null(_createdItem!.ProductBatchId);
        Assert.Null(_createdItem.ProductBatch);

        // No batch ID means no batch validation is needed.
        _repository.Verify(
            x => x.BatchIsActiveForProductAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _repository.Verify(
            x => x.ItemExistsAsync(
                UnitId,
                ProductId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyAddedOnce();
        VerifySavedOnce();
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("999999999999999.999")]
    public async Task Create_ValidBoundaryQuantity_Succeeds(
        string quantityText)
    {
        ConfigureValidCreation();

        var quantity = decimal.Parse(
            quantityText,
            CultureInfo.InvariantCulture);

        var request = ValidRequest(
            quantity: quantity,
            batchId: null);

        var result = await _service.CreateAsync(
            UnitId,
            request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(quantity, result!.Quantity);
        Assert.NotNull(_createdItem);
        Assert.Equal(quantity, _createdItem!.Quantity);

        VerifyAddedOnce();
        VerifySavedOnce();
    }

    [Fact]
    public async Task Create_ReloadFails_ThrowsInvalidOperation()
    {
        ConfigureValidCreation(reloadAfterSave: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(
                UnitId,
                ValidRequest(),
                CancellationToken.None));

        VerifyAddedOnce();
        VerifySavedOnce();
    }

    // ---------- Test helpers ----------

    private void ConfigurePallet(
        LogisticUnitStatus status = LogisticUnitStatus.Created)
    {
        var unit = new LogisticUnit
        {
            Id = UnitId,
            Code = "LE-010",
            Status = status
        };

        _repository
            .Setup(x => x.GetLogisticUnitAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);
    }

    private void ConfigureValidCreation(
        bool reloadAfterSave = true)
    {
        ConfigurePallet();

        _repository
            .Setup(x => x.IsGeneratedByGoodsReceiptAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.BatchIsActiveForProductAsync(
                BatchId,
                ProductId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.ItemExistsAsync(
                UnitId,
                ProductId,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.AddAsync(
                It.IsAny<LogisticUnitItem>(),
                It.IsAny<CancellationToken>()))
            .Callback<LogisticUnitItem, CancellationToken>(
                (item, _) =>
                {
                    item.Id = ItemId;
                    item.Product = TestProduct();

                    if (item.ProductBatchId.HasValue)
                    {
                        item.ProductBatch = new ProductBatch
                        {
                            Id = item.ProductBatchId.Value,
                            ProductId = ProductId,
                            BatchNumber = "BATCH-040",
                            IsActive = true
                        };
                    }

                    _createdItem = item;
                })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetByIdAsync(
                UnitId,
                ItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                reloadAfterSave ? _createdItem : null);
    }

    private static CreateLogisticUnitItemRequest ValidRequest(
        decimal quantity = 12.345m,
        int? batchId = BatchId)
    {
        return new CreateLogisticUnitItemRequest
        {
            ProductId = ProductId,
            ProductBatchId = batchId,
            Quantity = quantity
        };
    }

    private static Product TestProduct()
    {
        return new Product
        {
            Id = ProductId,
            Code = "ART-030",
            Name = "Test Product",
            IsActive = true
        };
    }

    private static LogisticUnitItem TestItem(
        int id = ItemId,
        int? batchId = BatchId,
        decimal quantity = 10.500m)
    {
        return new LogisticUnitItem
        {
            Id = id,
            LogisticUnitId = UnitId,
            ProductId = ProductId,
            Product = TestProduct(),
            ProductBatchId = batchId,
            ProductBatch = batchId.HasValue
                ? new ProductBatch
                {
                    Id = batchId.Value,
                    ProductId = ProductId,
                    BatchNumber = "BATCH-040",
                    IsActive = true
                }
                : null,
            Quantity = quantity
        };
    }

    private void VerifyNoAdd()
    {
        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<LogisticUnitItem>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyAddedOnce()
    {
        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<LogisticUnitItem>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
}
