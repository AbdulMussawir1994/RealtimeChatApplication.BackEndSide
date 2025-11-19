namespace App.Core.DTOs;

public class UserConnectionInfoDto
{
    public string ConnectionId { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsActive { get; set; }
}
