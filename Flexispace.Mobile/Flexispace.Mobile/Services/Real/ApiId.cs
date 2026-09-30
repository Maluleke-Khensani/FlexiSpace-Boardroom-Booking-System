namespace Flexispace.Mobile.Services.Real;

// Shared int<->Guid encoding for every prototype model whose Id the real
// API models as a plain int (Booking, AppNotification). Deterministic
// (not Guid.NewGuid()) so a Guid round-tripped through navigation
// parameters or stored in AppNotification.BookingId always decodes back
// to the same real id. Ported from Flexispace.Web's identical helper -
// see that project's Services/Real/ApiId.cs for the original commentary.
public static class ApiId
{
    public static Guid Encode(int id) => new(id, 0, 0, new byte[8]);

    public static int? Decode(Guid guid)
    {
        var bytes = guid.ToByteArray();
        for (var i = 4; i < 16; i++)
            if (bytes[i] != 0) return null;

        return BitConverter.ToInt32(bytes, 0);
    }
}
