using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Shipments;

public record ShipmentDto(
    int Id,
    string Number,
    int WarehouseOrderId,
    string WarehouseOrderNumber,
    string CarrierName,
    string? TrackingNumber,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ShippedAtUtc,
    int Revision
);
