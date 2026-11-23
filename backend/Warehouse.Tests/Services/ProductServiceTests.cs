
using Moq;
using Xunit;
using Warehouse.Application.DTOs.Products;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;

namespace Warehouse.Tests.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(_repository.Object);
    }

    [Fact]
    public async Task Create_DuplicateCode_ThrowsException()
    {
        var request = ValidRequest();

        _repository
            .Setup(x => x.CodeExistsAsync(
                "SKU-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateProductCodeException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_InactiveCategory_ThrowsException()
    {
        var request = ValidRequest();

        _repository
            .Setup(x => x.CodeExistsAsync(
                "SKU-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.CategoryIsActiveAsync(
                10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidProductReferenceException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_InactiveUnit_ThrowsException()
    {
        var request = ValidRequest();

        _repository
            .Setup(x => x.CodeExistsAsync(
                "SKU-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.CategoryIsActiveAsync(
                10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.UnitIsActiveAsync(
                20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidProductReferenceException>(
            () => _service.CreateAsync(
                request, CancellationToken.None));

        _repository.Verify(
            x => x.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_ValidProduct_NormalizesFields()
    {
        Product? addedProduct = null;

        _repository
            .Setup(x => x.CodeExistsAsync(
                "SKU-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository
            .Setup(x => x.CategoryIsActiveAsync(
                10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.UnitIsActiveAsync(
                20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repository
            .Setup(x => x.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) =>
            {
                product.Id = 55;

                product.ProductCategory = new ProductCategory
                {
                    Id = 10,
                    Code = "CAT",
                    Name = "Test Category"
                };

                product.UnitOfMeasure = new UnitOfMeasure
                {
                    Id = 20,
                    Code = "PCS",
                    Name = "Pieces",
                    Symbol = "pcs"
                };

                addedProduct = product;
            })
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetByIdAsync(
                55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => addedProduct);

        var request = new CreateProductRequest
        {
            Code = "  sku-001  ",
            Name = "  Test Product  ",
            Description = "  Description  ",
            Ean = "  590123  ",
            ProductCategoryId = 10,
            UnitOfMeasureId = 20
        };

        var result = await _service.CreateAsync(
            request, CancellationToken.None);

        Assert.Equal(55, result.Id);
        Assert.Equal("SKU-001", result.Code);
        Assert.Equal("Test Product", result.Name);
        Assert.Equal("Description", result.Description);
        Assert.Equal("590123", result.Ean);
        Assert.True(result.IsActive);

        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Deactivate_ExistingProduct_SetsInactive()
    {
        var product = new Product
        {
            Id = 55,
            IsActive = true
        };

        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.DeactivateAsync(
            55, CancellationToken.None);

        Assert.True(result);
        Assert.False(product.IsActive);

        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_DoesNotSaveAgain()
    {
        var product = new Product
        {
            Id = 55,
            IsActive = false
        };

        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _service.DeactivateAsync(
            55, CancellationToken.None);

        Assert.True(result);
        Assert.False(product.IsActive);

        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Deactivate_MissingProduct_ReturnsFalse()
    {
        _repository
            .Setup(x => x.GetTrackedByIdAsync(
                999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.DeactivateAsync(
            999, CancellationToken.None);

        Assert.False(result);

        _repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CreateProductRequest ValidRequest()
    {
        return new CreateProductRequest
        {
            Code = "SKU-001",
            Name = "Test Product",
            ProductCategoryId = 10,
            UnitOfMeasureId = 20
        };
    }
}
