using Flexispace.Core.Models;

namespace Flexispace.Core.Helpers;

/// <summary>
/// Role capabilities from FlexiTech Project Plan — Multi-Boardroom Booking System.
/// </summary>
public static class RolePermissions
{
    public static bool CanBookRooms(UserRole role) =>
        role is UserRole.Staff or UserRole.Client or UserRole.CentreManager or UserRole.Administrator;

    public static bool CanViewAvailability(UserRole role) =>
        role is UserRole.Staff or UserRole.CentreManager or UserRole.Administrator;

    public static bool CanCancelOwnBookings(UserRole role) =>
        role is UserRole.Staff or UserRole.Client or UserRole.CentreManager or UserRole.Administrator;

    public static bool CanManageLocationBookings(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static bool CanApproveBookings(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static bool CanBlockRooms(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static bool CanEditBookings(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static bool CanCancelAnyBooking(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static bool CanViewAllBookings(UserRole role) =>
        role is UserRole.Administrator or UserRole.CentreManager;

    public static bool CanManageRooms(UserRole role) =>
        role is UserRole.Administrator;

    public static bool CanConfigureLocations(UserRole role) =>
        role is UserRole.Administrator;

    public static bool CanManageUsers(UserRole role) =>
        role is UserRole.Administrator;

    // Matches the API: booking stats and the CSV export are open to Centre
    // Managers (their own location) and Administrators.
    public static bool CanViewReports(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    // The audit log (who changed what, when) is Administrator-only on the API.
    public static bool CanViewAuditLog(UserRole role) =>
        role is UserRole.Administrator;

    public static bool CanAccessManageHub(UserRole role) =>
        role is UserRole.CentreManager or UserRole.Administrator;

    public static string Describe(UserRole role) => role switch
    {
        UserRole.Administrator => "Add/remove rooms · configure locations · manage users · all bookings · reports",
        UserRole.CentreManager => "View, edit, block, and cancel bookings for your centre",
        UserRole.Staff => "Book rooms · cancel own bookings · view live availability",
        UserRole.Client => "Self-service booking across Flexispace locations",
        _ => string.Empty
    };

    public static string DisplayName(UserRole role) => role switch
    {
        UserRole.CentreManager => "Centre Manager",
        _ => role.ToString()
    };
}

