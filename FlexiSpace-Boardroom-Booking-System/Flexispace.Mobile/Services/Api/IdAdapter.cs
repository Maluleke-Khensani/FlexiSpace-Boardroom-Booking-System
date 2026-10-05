namespace Flexispace.Mobile.Services.Api;

/// <summary>
/// Maps API integer primary keys to Guid/string shapes used by existing mobile models.
/// </summary>
public static class IdAdapter
{
    public static Guid ToGuid(int id)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), id);
        return new Guid(bytes);
    }

    public static int ToInt(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt32(bytes, 0);
    }

    public static string ToStringId(int id) => id.ToString();

    public static bool TryParseInt(string? id, out int value) =>
        int.TryParse(id, out value);
}
