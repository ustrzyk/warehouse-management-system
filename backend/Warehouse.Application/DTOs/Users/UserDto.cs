using System;
using System.Collections.Generic;
using System.Text;


namespace Warehouse.Application.DTOs.Users;

public record UserDto(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles
);
