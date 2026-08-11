using Flexispace.Core.Models;

namespace Flexispace.Web.Helpers;

//UI formatting helpers for avatars, timestamps, notification styling, and image paths.
public static class DisplayHelpers
{
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }

    public static string TimeAgo(DateTime createdAt)
    {
        var span = DateTime.Now - createdAt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return createdAt.ToString("d MMM");
    }

    public static string NotificationIcon(string type) => type switch
    {
        "Confirmation" => "✓",
        "Reminder" => "!",
        "Cancellation" => "×",
        _ => "i"
    };

    public static string NotificationAccent(string type) => type switch
    {
        "Confirmation" => "#1E8E5A",
        "Reminder" => "#C4A035",
        "Cancellation" => "#C1392B",
        _ => "#3D5A73"
    };

    public static string JoinList(IEnumerable<string> items) =>
        items.Any() ? string.Join(", ", items) : "—";

    public static string ImageUrl(string? imageKey) =>
        string.IsNullOrEmpty(imageKey) ? "/images/placeholder.svg" : $"/images/{imageKey}";

    public static string FormatTime(TimeSpan time) =>
        $"{time.Hours:D2}:{time.Minutes:D2}";

    public static bool TryParseTime(string? value, out TimeSpan time) =>
        TimeSpan.TryParse(value, out time);
}
