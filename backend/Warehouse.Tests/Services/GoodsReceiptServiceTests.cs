
using System.Globalization;
using Moq;
using Xunit;
using Warehouse.Application.DTOs.GoodsReceipts;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class GoodsReceiptServiceTests
{
    private const int ReceiptId = 5;
    private const int WarehouseId = 1;
    private const int ItemId = 11;
    private const int ProductId = 100;

    private readonly Mock<IGoodsReceiptRepository> _repository = new();
    private readonly GoodsReceiptService _service;

    public GoodsReceiptServiceTests()
    {
        _service = new GoodsReceiptService(_repository.Object);
    }

    // ---------- Creating goods receipts ----------

    [Fact]
    public async Task Create_EmptyNumber_ThrowsException()
    {
        var request = ValidCreateRequest();
        request.Number = "   ";

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
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

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.CreateAsync(
                ValidCreateRequest(),
                CancellationToken.None));

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
                "GR-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = ValidCreateRequest();
        request.Number = "  gr-001  ";

        await Assert.ThrowsAsync<DuplicateGoodsReceiptException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<GoodsReceipt>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task Create_ValidReceipt_NormalizesAndSaves()
    {
        GoodsReceipt? addedReceipt = null;

        _repository
            .Setup(x => x.WarehouseIsActiveAsync(
                WarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.NumberExistsAsync(
                "GR-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.AddAsync(
                It.IsAny<GoodsReceipt>(),
                It.IsAny<CancellationToken>()))
            .Callback<GoodsReceipt, CancellationToken>((receipt, _) =>
            {
                receipt.Id = ReceiptId;
                receipt.Warehouse = TestWarehouse();
                addedReceipt = receipt;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetByIdAsync(
                ReceiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedReceipt);

        var request = new CreateGoodsReceiptRequest
        {
            Number = "  gr-001  ",
            WarehouseId = WarehouseId,
            SupplierName = "  Test Supplier  "
        };

        var result = await _service.CreateAsync(
            request, CancellationToken.None);

        Assert.Equal(ReceiptId, result.Id);
        Assert.Equal("GR-001", result.Number);
        Assert.Equal("Test Supplier", result.SupplierName);
        Assert.Equal("WH-01", result.WarehouseCode);
        Assert.Equal("Draft", result.Status);
        Assert.Null(result.ReceivedAtUtc);
        Assert.Null(result.LogisticUnitId);

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<GoodsReceipt>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    // ---------- Adding receipt items ----------

    [Fact]
    public async Task AddItem_MissingReceipt_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ReceiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoodsReceipt?)null);

        var result = await _service.AddItemAsync(
            ReceiptId,
            ValidItemRequest(),
            CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_ReceiptNotDraft_ThrowsException()
    {
        var receipt = TestReceipt(GoodsReceiptStatus.Received);

        ConfigureTrackedReceipt(receipt);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.AddItemAsync(
                ReceiptId,
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
        var receipt = TestReceipt();

        ConfigureTrackedReceipt(receipt);

        var request = ValidItemRequest();
        request.Quantity = decimal.Parse(
            quantityText, CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.AddItemAsync(
                ReceiptId,
                request,
                CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_InactiveProduct_ThrowsException()
    {
        var receipt = TestReceipt();

        ConfigureTrackedReceipt(receipt);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.AddItemAsync(
                ReceiptId,
                ValidItemRequest(),
                CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_InvalidBatch_ThrowsException()
    {
        var receipt = TestReceipt();

        ConfigureTrackedReceipt(receipt);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.BatchIsActiveForProductAsync(
                7,
                ProductId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = ValidItemRequest();
        request.ProductBatchId = 7;

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.AddItemAsync(
                ReceiptId,
                request,
                CancellationToken.None));

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_DuplicateItem_ThrowsException()
    {
        var receipt = TestReceipt();

        ConfigureTrackedReceipt(receipt);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.ItemExistsAsync(
                ReceiptId,
                ProductId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateGoodsReceiptException>(
            () => _service.AddItemAsync(
                ReceiptId,
                ValidItemRequest(),
                CancellationToken.None));

        _repository.Verify(
            x => x.AddItemAsync(
                It.IsAny<GoodsReceiptItem>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyNoSave();
    }

    [Fact]
    public async Task AddItem_Valid_IncrementsRevisionAndSaves()
    {
        var receipt = TestReceipt(withItem: false);

        ConfigureTrackedReceipt(receipt);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        GoodsReceiptItem? addedItem = null;

        _repository
            .Setup(x => x.AddItemAsync(
                It.IsAny<GoodsReceiptItem>(),
                It.IsAny<CancellationToken>()))
            .Callback<GoodsReceiptItem, CancellationToken>((item, _) =>
            {
                item.Id = ItemId;
                item.Product = TestProduct();
                addedItem = item;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetItemByIdAsync(
                ReceiptId,
                ItemId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedItem);

        var result = await _service.AddItemAsync(
            ReceiptId,
            ValidItemRequest(),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ItemId, result!.Id);
        Assert.Equal(ReceiptId, result.GoodsReceiptId);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal("SKU-001", result.ProductCode);
        Assert.Equal(10m, result.Quantity);
        Assert.Equal(3, receipt.Revision);

        _repository.Verify(
            x => x.AddItemAsync(
                It.IsAny<GoodsReceiptItem>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    // ---------- Confirming goods receipts ----------

    [Fact]
    public async Task Confirm_MissingReceipt_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetTrackedWithItemsAsync(
                ReceiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoodsReceipt?)null);

        var result = await _service.ConfirmAsync(
            ReceiptId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_AlreadyReceived_ThrowsException()
    {
        var receipt = TestReceipt(GoodsReceiptStatus.Received);

        ConfigureConfirmation(receipt);

        await Assert.ThrowsAsync<GoodsReceiptAlreadyConfirmedException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_ExistingPallet_ThrowsException()
    {
        var receipt = TestReceipt();
        receipt.LogisticUnitId = 99;

        ConfigureConfirmation(receipt);

        await Assert.ThrowsAsync<GoodsReceiptAlreadyConfirmedException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_InactiveWarehouse_ThrowsException()
    {
        var receipt = TestReceipt();
        receipt.Warehouse.IsActive = false;

        ConfigureConfirmation(receipt);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        Assert.Equal(GoodsReceiptStatus.Draft, receipt.Status);

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_EmptyReceipt_ThrowsException()
    {
        var receipt = TestReceipt(withItem: false);

        ConfigureConfirmation(receipt);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        Assert.Equal(GoodsReceiptStatus.Draft, receipt.Status);

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_InvalidQuantity_ThrowsException()
    {
        var receipt = TestReceipt();
        receipt.Items.Single().Quantity = -2m;

        ConfigureConfirmation(receipt);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        Assert.Equal(GoodsReceiptStatus.Draft, receipt.Status);

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_InactiveProduct_ThrowsException()
    {
        var receipt = TestReceipt();

        ConfigureConfirmation(receipt, productActive: false);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_InvalidBatch_ThrowsException()
    {
        var receipt = TestReceipt(batchId: 7);

        ConfigureConfirmation(receipt, batchActive: false);

        await Assert.ThrowsAsync<InvalidGoodsReceiptException>(
            () => _service.ConfirmAsync(
                ReceiptId, CancellationToken.None));

        VerifyNoPalletCreated();
        VerifyNoSave();
    }

    [Fact]
    public async Task Confirm_Valid_CreatesPalletAndMarksReceived()
    {
        var receipt = TestReceipt();

        ConfigureConfirmation(receipt);

        LogisticUnit? addedPallet = null;

        _repository
            .Setup(x => x.AddLogisticUnitAsync(
                It.IsAny<LogisticUnit>(),
                It.IsAny<CancellationToken>()))
            .Callback<LogisticUnit, CancellationToken>((pallet, _) =>
            {
                addedPallet = pallet;
                pallet.Id = 88;
            })
            .Returns(Task.CompletedTask);

        // Simulate EF Core assigning the foreign key
        // after saving the new pallet to the database.
        _repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                receipt.LogisticUnitId = 88;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.ConfirmAsync(
            ReceiptId, CancellationToken.None);

        Assert.NotNull(result);

        Assert.Equal("Received", result!.Status);
        Assert.Equal(88, result.LogisticUnitId);
        Assert.NotNull(result.ReceivedAtUtc);

        Assert.Equal(
            GoodsReceiptStatus.Received,
            receipt.Status);

        Assert.Equal(3, receipt.Revision);

        Assert.NotNull(addedPallet);
        Assert.Same(addedPallet, receipt.LogisticUnit);

        Assert.Equal("LE-GR-00000005-AUTO", addedPallet!.Code);
        Assert.Equal(LogisticUnitStatus.Created, addedPallet.Status);
        Assert.Null(addedPallet.StorageLocationId);

        var palletItem = Assert.Single(addedPallet.Items);

        Assert.Equal(ProductId, palletItem.ProductId);
        Assert.Equal(10m, palletItem.Quantity);
        Assert.Null(palletItem.ProductBatchId);

        _repository.Verify(
            x => x.AddLogisticUnitAsync(
                It.IsAny<LogisticUnit>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySavedOnce();
    }

    // ---------- Test helpers ----------

    private void ConfigureTrackedReceipt(GoodsReceipt receipt)
    {
        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                ReceiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
    }

    private void ConfigureConfirmation(
        GoodsReceipt receipt,
        bool productActive = true,
        bool batchActive = true)
    {
        _repository
            .Setup(x => x.GetTrackedWithItemsAsync(
                ReceiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        _repository
            .Setup(x => x.ProductIsActiveAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productActive);

        _repository
            .Setup(x => x.BatchIsActiveForProductAsync(
                It.IsAny<int>(),
                ProductId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchActive);
    }

    private void VerifyNoPalletCreated()
    {
        _repository.Verify(
            x => x.AddLogisticUnitAsync(
                It.IsAny<LogisticUnit>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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

    private static CreateGoodsReceiptRequest ValidCreateRequest()
    {
        return new CreateGoodsReceiptRequest
        {
            Number = "GR-001",
            WarehouseId = WarehouseId,
            SupplierName = "Test Supplier"
        };
    }

    private static AddGoodsReceiptItemRequest ValidItemRequest()
    {
        return new AddGoodsReceiptItemRequest
        {
            ProductId = ProductId,
            Quantity = 10m
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

    private static GoodsReceipt TestReceipt(
        GoodsReceiptStatus status = GoodsReceiptStatus.Draft,
        bool withItem = true,
        int? batchId = null)
    {
        var receipt = new GoodsReceipt
        {
            Id = ReceiptId,
            Number = "GR-005",
            WarehouseId = WarehouseId,
            Warehouse = TestWarehouse(),
            Status = status,
            Revision = 2
        };

        if (withItem)
        {
            receipt.Items.Add(new GoodsReceiptItem
            {
                Id = ItemId,
                GoodsReceiptId = ReceiptId,
                GoodsReceipt = receipt,
                ProductId = ProductId,
                Product = TestProduct(),
                ProductBatchId = batchId,
                Quantity = 10m
            });
        }

        return receipt;
    }
}
