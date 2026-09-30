namespace Flexispace.Web.Services.Real;

// Shared int<->Guid encoding for every prototype model whose Id the real
// API models as a plain int (Booking, AppNotification). Deterministic
// (not Guid.NewGuid()) so a Guid round-tripped through a route parameter
// or stored in AppNotification.BookingId always decodes back to the same
// real id. Not used for User/Boardroom/Location, which use their real int
// id's string form directly instead (see RealAuthService/RealRoomService)
// - Booking/AppNotification specifically need a Guid because that's the
// prototype model's declared property type.
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
