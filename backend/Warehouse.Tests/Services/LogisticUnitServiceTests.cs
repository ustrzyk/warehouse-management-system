
using Moq;
using Xunit;
using Warehouse.Application.DTOs.LogisticUnits;
using Warehouse.Application.DTOs.LogisticUnitMovements;
using Warehouse.Application.Exceptions;
using Warehouse.Application.Interfaces.Repositories;
using Warehouse.Application.Services;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Tests.Services;

public class LogisticUnitServiceTests
{
    private const int UnitId = 10;
    private const int FromLocationId = 20;
    private const int ToLocationId = 30;
    private const int MovementId = 40;

    private readonly Mock<ILogisticUnitRepository> _repository = new();
    private readonly LogisticUnitService _service;

    public LogisticUnitServiceTests()
    {
        _service = new LogisticUnitService(_repository.Object);
    }

    // ---------- Reading logistic units ----------

    [Fact]
    public async Task GetPage_ReturnsMappedItemsAndPagination()
    {
        var query = new LogisticUnitListQuery { Page = 2, PageSize = 5 };
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);

        _repository.Setup(x => x.GetPageAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<LogisticUnit>)new[] { unit }, 11));

        var result = await _service.GetPageAsync(query, CancellationToken.None);

        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        var dto = Assert.Single(result.Items);
        Assert.Equal(UnitId, dto.Id);
        Assert.Equal("LE-010", dto.Code);
        Assert.Equal("Stored", dto.Status);
        Assert.Equal("A-01", dto.StorageLocationCode);
        VerifyNoSave();
    }

    [Fact]
    public async Task GetById_MissingUnit_ReturnsNull()
    {
        _repository.Setup(x => x.GetByIdAsync(UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.GetByIdAsync(UnitId, CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Fact]
    public async Task GetById_ExistingUnit_ReturnsDto()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);

        _repository.Setup(x => x.GetByIdAsync(UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);

        var result = await _service.GetByIdAsync(UnitId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(UnitId, result!.Id);
        Assert.Equal("LE-010", result.Code);
        Assert.Equal("Stored", result.Status);
        Assert.Equal(FromLocationId, result.StorageLocationId);
        Assert.Equal("A-01", result.StorageLocationCode);
        Assert.Equal(unit.CreatedAtUtc, result.CreatedAtUtc);
        VerifyNoSave();
    }

    // ---------- Creating a logistic unit ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankCode_ThrowsException(string code)
    {
        var request = new CreateLogisticUnitRequest { Code = code };

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.CreateAsync(request, CancellationToken.None));

        _repository.Verify(x => x.CodeExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_DuplicateNormalizedCode_ThrowsException()
    {
        _repository.Setup(x => x.CodeExistsAsync(
                "LE-010", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateLogisticUnitCodeException>(
            () => _service.CreateAsync(
                new CreateLogisticUnitRequest { Code = " le-010 " },
                CancellationToken.None));

        _repository.Verify(x => x.AddAsync(
            It.IsAny<LogisticUnit>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Create_Valid_NormalizesCodeAndSaves()
    {
        LogisticUnit? created = null;

        _repository.Setup(x => x.CodeExistsAsync(
                "LE-010", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repository.Setup(x => x.AddAsync(
                It.IsAny<LogisticUnit>(), It.IsAny<CancellationToken>()))
            .Callback<LogisticUnit, CancellationToken>((unit, _) =>
            {
                unit.Id = UnitId;
                created = unit;
            })
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        var result = await _service.CreateAsync(
            new CreateLogisticUnitRequest { Code = "  le-010  " },
            CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.NotNull(created);
        Assert.Equal(UnitId, result.Id);
        Assert.Equal("LE-010", result.Code);
        Assert.Equal("Created", result.Status);
        Assert.Null(result.StorageLocationId);
        Assert.Null(result.StorageLocationCode);
        Assert.InRange(result.CreatedAtUtc, before, after);
        Assert.Equal(LogisticUnitStatus.Created, created!.Status);

        _repository.Verify(x => x.AddAsync(
            It.IsAny<LogisticUnit>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifySavedOnce();
    }

    // ---------- Storing a pallet ----------

    [Fact]
    public async Task Store_MissingUnit_ReturnsNull()
    {
        _repository.Setup(x => x.GetTrackedByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.StoreAsync(
            UnitId, StoreRequest(), CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Theory]
    [InlineData(LogisticUnitStatus.Stored)]
    [InlineData(LogisticUnitStatus.Picking)]
    [InlineData(LogisticUnitStatus.Shipped)]
    public async Task Store_UnitNotCreated_ThrowsException(LogisticUnitStatus status)
    {
        var unit = TestUnit(status);
        SetupTrackedUnit(unit);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.StoreAsync(
                UnitId, StoreRequest(), CancellationToken.None));

        _repository.Verify(x => x.HasItemsAsync(
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Store_EmptyUnit_ThrowsException()
    {
        var unit = TestUnit();
        SetupTrackedUnit(unit);
        _repository.Setup(x => x.HasItemsAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.StoreAsync(
                UnitId, StoreRequest(), CancellationToken.None));

        Assert.Equal(LogisticUnitStatus.Created, unit.Status);
        Assert.Null(unit.StorageLocationId);
        _repository.Verify(x => x.LocationIsAllowedForUnitAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Store_DisallowedLocation_ThrowsException()
    {
        var unit = TestUnit();
        SetupTrackedUnit(unit);
        _repository.Setup(x => x.HasItemsAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(x => x.LocationIsAllowedForUnitAsync(
                UnitId, ToLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.StoreAsync(
                UnitId, StoreRequest(), CancellationToken.None));

        Assert.Equal(LogisticUnitStatus.Created, unit.Status);
        Assert.Null(unit.StorageLocationId);
        VerifyNoSave();
    }

    [Fact]
    public async Task Store_Valid_ChangesStatusAndLocationAndSaves()
    {
        var unit = TestUnit();
        SetupAllowedStore(unit);
        var reloaded = TestUnit(LogisticUnitStatus.Stored, ToLocationId);
        _repository.Setup(x => x.GetByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reloaded);

        var result = await _service.StoreAsync(
            UnitId, StoreRequest(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(LogisticUnitStatus.Stored, unit.Status);
        Assert.Equal(ToLocationId, unit.StorageLocationId);
        Assert.Equal("Stored", result!.Status);
        Assert.Equal(ToLocationId, result.StorageLocationId);
        Assert.Equal("B-01", result.StorageLocationCode);
        VerifySavedOnce();
    }

    [Fact]
    public async Task Store_ReloadFails_ThrowsInvalidOperation()
    {
        var unit = TestUnit();
        SetupAllowedStore(unit);
        _repository.Setup(x => x.GetByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StoreAsync(
                UnitId, StoreRequest(), CancellationToken.None));

        VerifySavedOnce();
    }

    // ---------- Moving a pallet ----------

    [Fact]
    public async Task Move_MissingUnit_ReturnsNull()
    {
        _repository.Setup(x => x.GetTrackedByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.MoveAsync(
            UnitId, MoveRequest(), CancellationToken.None);

        Assert.Null(result);
        VerifyNoSave();
    }

    [Theory]
    [InlineData(LogisticUnitStatus.Created, true)]
    [InlineData(LogisticUnitStatus.Picking, true)]
    [InlineData(LogisticUnitStatus.Shipped, true)]
    [InlineData(LogisticUnitStatus.Stored, false)]
    public async Task Move_InvalidStatusOrMissingLocation_ThrowsException(
        LogisticUnitStatus status, bool hasLocation)
    {
        var unit = TestUnit(status, hasLocation ? FromLocationId : null);
        SetupTrackedUnit(unit);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.MoveAsync(
                UnitId, MoveRequest(), CancellationToken.None));

        _repository.Verify(x => x.GetValidMoveDestinationAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Move_SameLocation_ThrowsException()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        SetupTrackedUnit(unit);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.MoveAsync(
                UnitId,
                new MoveLogisticUnitRequest { ToStorageLocationId = FromLocationId },
                CancellationToken.None));

        _repository.Verify(x => x.GetValidMoveDestinationAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoSave();
    }

    [Fact]
    public async Task Move_InvalidDestination_ThrowsException()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        SetupTrackedUnit(unit);
        _repository.Setup(x => x.GetValidMoveDestinationAsync(
                FromLocationId, ToLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageLocation?)null);

        await Assert.ThrowsAsync<InvalidLogisticUnitException>(
            () => _service.MoveAsync(
                UnitId, MoveRequest(), CancellationToken.None));

        Assert.Equal(FromLocationId, unit.StorageLocationId);
        VerifyNoSave();
    }

    [Fact]
    public async Task Move_MissingSourceLocationCode_ThrowsInvalidOperation()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        unit.StorageLocation = null;
        SetupAllowedMove(unit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MoveAsync(
                UnitId, MoveRequest(), CancellationToken.None));

        Assert.Equal(FromLocationId, unit.StorageLocationId);
        VerifyNoSave();
    }

    [Fact]
    public async Task Move_Valid_SavesMovementAndTrimsNote()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        SetupAllowedMove(unit);
        LogisticUnitMovement? created = null;
        SetupCreatedMovement(unit, movement => created = movement);

        var before = DateTime.UtcNow;
        var result = await _service.MoveAsync(
            UnitId, MoveRequest("  check label  "), CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.NotNull(created);
        Assert.NotNull(result);
        Assert.Equal(ToLocationId, unit.StorageLocationId);
        Assert.Equal(LogisticUnitStatus.Stored, unit.Status);
        Assert.Equal(MovementId, result!.Id);
        Assert.Equal(UnitId, result.LogisticUnitId);
        Assert.Equal("LE-010", result.LogisticUnitCode);
        Assert.Equal(FromLocationId, result.FromStorageLocationId);
        Assert.Equal("A-01", result.FromLocationCode);
        Assert.Equal(ToLocationId, result.ToStorageLocationId);
        Assert.Equal("B-01", result.ToLocationCode);
        Assert.Equal("check label", result.Note);
        Assert.InRange(result.MovedAtUtc, before, after);
        Assert.Equal("check label", created!.Note);
        VerifyMovementAddedOnce();
        VerifySavedOnce();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Move_BlankNote_IsSavedAsNull(string? note)
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        SetupAllowedMove(unit);
        SetupCreatedMovement(unit);

        var result = await _service.MoveAsync(
            UnitId, MoveRequest(note), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.Note);
        VerifyMovementAddedOnce();
        VerifySavedOnce();
    }

    [Fact]
    public async Task Move_ReloadMovementFails_ThrowsInvalidOperation()
    {
        var unit = TestUnit(LogisticUnitStatus.Stored, FromLocationId);
        SetupAllowedMove(unit);
        _repository.Setup(x => x.AddMovementAsync(
                It.IsAny<LogisticUnitMovement>(), It.IsAny<CancellationToken>()))
            .Callback<LogisticUnitMovement, CancellationToken>((movement, _) =>
                movement.Id = MovementId)
            .Returns(Task.CompletedTask);
        _repository.Setup(x => x.GetMovementByIdAsync(
                UnitId, MovementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnitMovement?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MoveAsync(
                UnitId, MoveRequest(), CancellationToken.None));

        Assert.Equal(ToLocationId, unit.StorageLocationId);
        VerifyMovementAddedOnce();
        VerifySavedOnce();
    }

    // ---------- Movement history ----------

    [Fact]
    public async Task GetMovementsPage_MissingUnit_ReturnsNull()
    {
        var query = new LogisticUnitMovementListQuery();
        _repository.Setup(x => x.GetByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnit?)null);

        var result = await _service.GetMovementsPageAsync(
            UnitId, query, CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(x => x.GetMovementsPageAsync(
            It.IsAny<int>(), It.IsAny<LogisticUnitMovementListQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMovementsPage_ExistingUnit_ReturnsMappedPage()
    {
        var query = new LogisticUnitMovementListQuery { Page = 3, PageSize = 5 };
        var movement = TestMovement();

        _repository.Setup(x => x.GetByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestUnit());
        _repository.Setup(x => x.GetMovementsPageAsync(
                UnitId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<LogisticUnitMovement>)new[] { movement }, 13));

        var result = await _service.GetMovementsPageAsync(
            UnitId, query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(13, result!.TotalCount);
        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.PageSize);
        var dto = Assert.Single(result.Items);
        Assert.Equal(MovementId, dto.Id);
        Assert.Equal("LE-010", dto.LogisticUnitCode);
        Assert.Equal("A-01", dto.FromLocationCode);
        Assert.Equal("B-01", dto.ToLocationCode);
        Assert.Equal("check label", dto.Note);
    }

    [Fact]
    public async Task GetMovementById_MissingMovement_ReturnsNull()
    {
        _repository.Setup(x => x.GetMovementByIdAsync(
                UnitId, MovementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LogisticUnitMovement?)null);

        var result = await _service.GetMovementByIdAsync(
            UnitId, MovementId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMovementById_ExistingMovement_ReturnsSnapshot()
    {
        var movement = TestMovement();
        _repository.Setup(x => x.GetMovementByIdAsync(
                UnitId, MovementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);

        var result = await _service.GetMovementByIdAsync(
            UnitId, MovementId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(MovementId, result!.Id);
        Assert.Equal(UnitId, result.LogisticUnitId);
        Assert.Equal("LE-010", result.LogisticUnitCode);
        Assert.Equal(FromLocationId, result.FromStorageLocationId);
        Assert.Equal(ToLocationId, result.ToStorageLocationId);
        Assert.Equal("A-01", result.FromLocationCode);
        Assert.Equal("B-01", result.ToLocationCode);
        Assert.Equal(movement.MovedAtUtc, result.MovedAtUtc);
    }

    // ---------- Test helpers ----------

    private static LogisticUnit TestUnit(
        LogisticUnitStatus status = LogisticUnitStatus.Created,
        int? locationId = null)
    {
        return new LogisticUnit
        {
            Id = UnitId,
            Code = "LE-010",
            Status = status,
            StorageLocationId = locationId,
            StorageLocation = locationId.HasValue
                ? TestLocation(locationId.Value,
                    locationId.Value == FromLocationId ? "A-01" : "B-01")
                : null,
            CreatedAtUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        };
    }

    private static StorageLocation TestLocation(int id, string code)
    {
        return new StorageLocation { Id = id, Code = code, IsActive = true };
    }

    private static StoreLogisticUnitRequest StoreRequest()
    {
        return new StoreLogisticUnitRequest { StorageLocationId = ToLocationId };
    }

    private static MoveLogisticUnitRequest MoveRequest(string? note = "check label")
    {
        return new MoveLogisticUnitRequest
        {
            ToStorageLocationId = ToLocationId,
            Note = note
        };
    }

    private static LogisticUnitMovement TestMovement()
    {
        return new LogisticUnitMovement
        {
            Id = MovementId,
            LogisticUnitId = UnitId,
            LogisticUnit = TestUnit(LogisticUnitStatus.Stored, ToLocationId),
            FromStorageLocationId = FromLocationId,
            FromLocationCode = "A-01",
            ToStorageLocationId = ToLocationId,
            ToLocationCode = "B-01",
            Note = "check label",
            MovedAtUtc = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)
        };
    }

    private void SetupTrackedUnit(LogisticUnit unit)
    {
        _repository.Setup(x => x.GetTrackedByIdAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);
    }

    private void SetupAllowedStore(LogisticUnit unit)
    {
        SetupTrackedUnit(unit);
        _repository.Setup(x => x.HasItemsAsync(
                UnitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(x => x.LocationIsAllowedForUnitAsync(
                UnitId, ToLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupAllowedMove(LogisticUnit unit)
    {
        SetupTrackedUnit(unit);
        _repository.Setup(x => x.GetValidMoveDestinationAsync(
                FromLocationId, ToLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestLocation(ToLocationId, "B-01"));
    }

    private void SetupCreatedMovement(
        LogisticUnit unit,
        Action<LogisticUnitMovement>? onCreate = null)
    {
        LogisticUnitMovement? created = null;
        _repository.Setup(x => x.AddMovementAsync(
                It.IsAny<LogisticUnitMovement>(), It.IsAny<CancellationToken>()))
            .Callback<LogisticUnitMovement, CancellationToken>((movement, _) =>
            {
                movement.Id = MovementId;
                movement.LogisticUnit = unit;
                created = movement;
                onCreate?.Invoke(movement);
            })
            .Returns(Task.CompletedTask);

        _repository.Setup(x => x.GetMovementByIdAsync(
                UnitId, MovementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => created);
    }

    private void VerifyNoSave()
    {
        _repository.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifySavedOnce()
    {
        _repository.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyMovementAddedOnce()
    {
        _repository.Verify(x => x.AddMovementAsync(
            It.IsAny<LogisticUnitMovement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
