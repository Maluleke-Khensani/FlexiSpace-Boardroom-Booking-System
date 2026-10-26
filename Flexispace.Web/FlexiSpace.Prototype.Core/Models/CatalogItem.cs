namespace Flexispace.Core.Models;

public sealed class CatalogItem
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
