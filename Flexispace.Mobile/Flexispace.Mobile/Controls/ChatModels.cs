using System.Collections.ObjectModel;

namespace Flexispace.Mobile.Controls;

public class ChatLine
{
    public string Text { get; init; } = string.Empty;
    public bool IsUser { get; init; }
}

public class FaqItem
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}
