using System.Globalization;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Converters;

public class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.55 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class StepEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int step && parameter is not null && int.TryParse(parameter.ToString(), out var target))
            return step == target;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the wizard step is at least the integer ConverterParameter.</summary>
public class IntAtLeastConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int step && parameter is not null && int.TryParse(parameter.ToString(), out var target))
            return step >= target;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the wizard step is strictly below the integer ConverterParameter.</summary>
public class IntBelowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int step && parameter is not null && int.TryParse(parameter.ToString(), out var target))
            return step < target;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// MultiBinding: item id + currently selected id. Parameter bg|fg picks chip colours.
/// </summary>
public class ChipSelectedConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = values.Length >= 2 &&
                 string.Equals(values[0]?.ToString() ?? string.Empty, values[1]?.ToString() ?? string.Empty, StringComparison.Ordinal);
        return parameter?.ToString() switch
        {
            "fg" => on ? Color.FromArgb("#111111") : Color.FromArgb("#1A1A1A"),
            "wash" => on ? Color.FromArgb("#26C4A035") : Colors.White,
            "stroke" => on ? Color.FromArgb("#C4A035") : Color.FromArgb("#E4DFD6"),
            "bool" => on,
            _ => on ? Color.FromArgb("#C4A035") : Colors.Transparent
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class JoinListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<string> list ? string.Join(" · ", list) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a notification's Type to a glyph shown in its accent badge.</summary>
public class NotificationIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Confirmation" => "\ue86c", // check_circle
            "Cancellation" => "\ue5cd", // close
            "Reminder" => "\ue8b5",     // schedule
            _ => "\ue88e"               // info
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a notification's Type to a brand accent colour for its badge.</summary>
public class NotificationAccentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "Confirmation" => "BrandSuccess",
            "Cancellation" => "BrandDanger",
            "Reminder" => "BrandTeal",
            _ => "BrandSteel"
        };
        return Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a timestamp as a short relative label (e.g. "12m ago", "3d ago").</summary>
public class TimeAgoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;
        var span = DateTime.Now - dt;
        if (span.TotalSeconds < 45) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("d MMM");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Flips the swipe-to-toggle label depending on read state.</summary>
public class ReadToggleTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Mark unread" : "Mark read";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a booking/room status word to a brand accent colour used for pills and edge bars.</summary>
public class StatusAccentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() switch
        {
            "Confirmed" => "BrandSuccess",
            "Pending" => "BrandGold",
            "Cancelled" => "BrandDanger",
            "Completed" => "BrandMuted",
            _ => "BrandMuted"
        };
        return Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Derives up to two initials from a display name for avatar badges (e.g. "Thabo Ndlovu" → "TN").</summary>
public class InitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a <see cref="UserRole"/> to the friendly role label.</summary>
public class RoleDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UserRole role)
            return RolePermissions.DisplayName(role);
        if (value is string s && Enum.TryParse<UserRole>(s, out var parsed))
            return RolePermissions.DisplayName(parsed);
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
