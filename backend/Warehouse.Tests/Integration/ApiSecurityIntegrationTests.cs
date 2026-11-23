
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Warehouse.Api.Controllers;
using Warehouse.Application.DTOs.Authentication;
using Warehouse.Application.DTOs.Products;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Services;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Tests.Integration;

public class ApiSecurityIntegrationTests : IDisposable
{
    private const string TestEmail = "integration@example.test";
    private const string TestPassword = "IntegrationOnly-123!";
    private const int ProductId = 7;

    private readonly ApplicationUser _user = new()
    {
        Id = "integration-user-1",
        UserName = TestEmail,
        Email = TestEmail,
        FullName = "Integration Test User",
        IsActive = true
    };

    private readonly Mock<IProductService> _productService = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;

    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    private IList<string> _roles =
        new List<string> { WarehouseRoles.WarehouseWorker };

    private string _securityStamp = "test-security-stamp-1";
    private bool _accountExists = true;

    public ApiSecurityIntegrationTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();

        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance
        );

        _userManager
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) =>
                email.Equals(
                    TestEmail,
                    StringComparison.OrdinalIgnoreCase)
                    ? _user
                    : null);

        _userManager
            .Setup(x => x.IsLockedOutAsync(
                It.IsAny<ApplicationUser>()))
            .ReturnsAsync(false);

        _userManager
            .Setup(x => x.CheckPasswordAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser _, string password) =>
                password == TestPassword);

        _userManager
            .Setup(x => x.ResetAccessFailedCountAsync(
                It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManager
            .Setup(x => x.GetRolesAsync(
                It.IsAny<ApplicationUser>()))
            .ReturnsAsync(() => _roles);

        _userManager
            .Setup(x => x.GetSecurityStampAsync(
                It.IsAny<ApplicationUser>()))
            .ReturnsAsync(() => _securityStamp);

        _userManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) =>
                _accountExists && id == _user.Id
                    ? _user
                    : null);

        _factory = new TestApiFactory(
            _userManager.Object,
            _productService.Object);

        _client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    // ---------- Anonymous requests ----------

    [Fact]
    public async Task Products_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            ValidProductRequest());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        VerifyProductNotCreated();
    }

    [Fact]
    public async Task Products_InvalidToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", "invalid.jwt.token");

        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ---------- Login and JWT ----------

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = "unknown@example.test",
                Password = TestPassword
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtAndAllowsMe()
    {
        var login = await LoginAsAsync(
            WarehouseRoles.Manager);

        Assert.NotEmpty(login.AccessToken);
        Assert.Equal("Bearer", login.TokenType);
        Assert.Contains(WarehouseRoles.Manager, login.Roles);

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains(_user.Id, json);
        Assert.Contains(TestEmail, json);
    }

    [Fact]
    public async Task Jwt_ChangedSecurityStamp_Returns401()
    {
        await LoginAsAsync(WarehouseRoles.Manager);

        _securityStamp = "test-security-stamp-2";

        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Jwt_DeactivatedAccount_Returns401()
    {
        await LoginAsAsync(WarehouseRoles.Manager);

        _user.IsActive = false;

        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Jwt_DeletedAccount_Returns401()
    {
        await LoginAsAsync(WarehouseRoles.Manager);

        _accountExists = false;

        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ---------- Role-based permissions ----------

    [Fact]
    public async Task WarehouseWorker_CanReadProduct()
    {
        await LoginAsAsync(WarehouseRoles.WarehouseWorker);

        _productService
            .Setup(x => x.GetByIdAsync(
                ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestProductDto());

        var response = await _client.GetAsync(
            $"/api/products/{ProductId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product =
            await response.Content.ReadFromJsonAsync<ProductDto>();

        Assert.NotNull(product);
        Assert.Equal("SKU-007", product!.Code);
    }

    [Fact]
    public async Task WarehouseWorker_CannotCreateProduct()
    {
        await LoginAsAsync(WarehouseRoles.WarehouseWorker);

        var response = await _client.PostAsJsonAsync(
            "/api/products",
            ValidProductRequest());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        VerifyProductNotCreated();
    }

    [Theory]
    [InlineData(WarehouseRoles.Administrator)]
    [InlineData(WarehouseRoles.Manager)]
    public async Task CatalogEditor_CanCreateProduct(
        string role)
    {
        await LoginAsAsync(role);

        _productService
            .Setup(x => x.CreateAsync(
                It.IsAny<CreateProductRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestProductDto());

        var response = await _client.PostAsJsonAsync(
            "/api/products",
            ValidProductRequest());

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var product =
            await response.Content.ReadFromJsonAsync<ProductDto>();

        Assert.NotNull(product);
        Assert.Equal(ProductId, product!.Id);
        Assert.Equal("SKU-007", product.Code);
    }

    [Fact]
    public async Task Manager_CannotAccessUserManagement()
    {
        await LoginAsAsync(WarehouseRoles.Manager);

        var response = await _client.GetAsync("/api/users");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ---------- Validation and HTTP errors ----------

    [Fact]
    public async Task CreateProduct_InvalidModel_Returns400()
    {
        await LoginAsAsync(WarehouseRoles.Administrator);

        var request = ValidProductRequest();
        request.Code = "";
        request.Name = "";

        var response = await _client.PostAsJsonAsync(
            "/api/products",
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        VerifyProductNotCreated();
    }

    [Fact]
    public async Task CreateProduct_DuplicateCode_Returns409()
    {
        await LoginAsAsync(WarehouseRoles.Administrator);

        _productService
            .Setup(x => x.CreateAsync(
                It.IsAny<CreateProductRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateProductCodeException());

        var response = await _client.PostAsJsonAsync(
            "/api/products",
            ValidProductRequest());

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_InvalidReference_Returns400()
    {
        await LoginAsAsync(WarehouseRoles.Administrator);

        _productService
            .Setup(x => x.CreateAsync(
                It.IsAny<CreateProductRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidProductReferenceException(
                    "Invalid product category."));

        var response = await _client.PostAsJsonAsync(
            "/api/products",
            ValidProductRequest());

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // ---------- Helpers ----------

    private async Task<LoginResponse> LoginAsAsync(string role)
    {
        _roles = new List<string> { role };

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = TestEmail,
                Password = TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);
        Assert.NotEmpty(login!.AccessToken);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        return login;
    }

    private void VerifyProductNotCreated()
    {
        _productService.Verify(
            x => x.CreateAsync(
                It.IsAny<CreateProductRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CreateProductRequest ValidProductRequest()
    {
        return new CreateProductRequest
        {
            Code = "SKU-007",
            Name = "Test Product",
            ProductCategoryId = 1,
            UnitOfMeasureId = 1
        };
    }

    private static ProductDto TestProductDto()
    {
        return new ProductDto(
            ProductId,
            "SKU-007",
            "Test Product",
            null,
            null,
            1,
            "CAT",
            "Category",
            1,
            "PCS",
            "Pieces",
            "pcs",
            true);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- Integration test server ----------

    private sealed class TestApiFactory
        : WebApplicationFactory<ProductsController>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProductService _productService;

        public TestApiFactory(
            UserManager<ApplicationUser> userManager,
            IProductService productService)
        {
            _userManager = userManager;
            _productService = productService;
        }

        // IMPORTANT:
        // Supply settings BEFORE Program.cs reads them.
        //
        // ConfigureAppConfiguration in ConfigureWebHost
        // runs too late for values read directly from
        // WebApplication.CreateBuilder(args).
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:WarehouseDb"] =
                            "Server=(local);" +
                            "Database=WarehouseIntegrationTestsNeverUsed;" +
                            "Trusted_Connection=True;" +
                            "TrustServerCertificate=True;",

                        ["Jwt:Key"] = new string('T', 64),
                        ["Jwt:Issuer"] = "Warehouse.Tests",
                        ["Jwt:Audience"] = "Warehouse.Tests"
                    });
            });

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            // Do not run development data seeding.
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<UserManager<ApplicationUser>>();
                services.AddSingleton(_userManager);

                services.RemoveAll<IProductService>();
                services.AddSingleton(_productService);
            });
        }
    }
}
