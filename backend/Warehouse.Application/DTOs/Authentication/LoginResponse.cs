using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Authentication;

public record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string FullName,
    IReadOnlyList<string> Roles
);
