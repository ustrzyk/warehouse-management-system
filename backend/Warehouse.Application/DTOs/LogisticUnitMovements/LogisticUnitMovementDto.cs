using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.LogisticUnitMovements;

public record LogisticUnitMovementDto(
    int Id,
    int LogisticUnitId,
    string LogisticUnitCode,
    int FromStorageLocationId,
    string FromLocationCode,
    int ToStorageLocationId,
    string ToLocationCode,
    DateTime MovedAtUtc,
    string? Note
);
