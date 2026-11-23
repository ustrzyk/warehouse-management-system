
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Application.DTOs.Users;

public class SetUserActiveRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
