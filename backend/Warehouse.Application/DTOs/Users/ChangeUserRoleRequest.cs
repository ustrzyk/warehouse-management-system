
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Users;

public class ChangeUserRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}
