namespace Flexispace.Core.Models;

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
    public string Type { get; set; } = "Info";
    public Guid? BookingId { get; set; }
}

