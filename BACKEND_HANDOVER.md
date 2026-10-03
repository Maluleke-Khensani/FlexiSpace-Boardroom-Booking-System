# FlexiSpace Backend – Technical Handover Document

**For:** Razor Frontend Integration & MAUI Mobile App Integration Teams  
**Date:** October 2026  
**Prepared by:** Khensani (Backend Lead)

---

## 1. Architecture & Tech Stack

### Core Stack
- **Runtime:** .NET 8 Web API
- **Database:** Microsoft SQL Server (Azure SQL) with Entity Framework Core (EF Core)
- **Authentication:** Microsoft Entra ID (Azure AD) with JWT bearer tokens
- **Authorization:** Role-Based Access Control (RBAC) via custom claims + database roles
- **Email/Calendar:** Microsoft Graph API (requires M365 tenant credentials)
- **AI Recommendations:** Groq AI API (GroqAI)

### Project Structure
```
FlexiSpace-Boardroom-Booking-System/FlexiSpace/
├── FlexiSpace.API/              # API controllers, Program.cs, auth setup
├── FlexiSpace.Core/             # Domain entities, interfaces, DTOs, enums
├── FlexiSpace.Infrastructure/   # EF Core DbContext, service implementations, seed data
├── Flexispace.Web/              # Blazor Server prototype (Khumo's work)
└── Flexispace.Mobile/           # MAUI mobile app (Riba's work — stub interfaces only)
```

---

## 2. Data Model & Entity Relationships

### Core Entities

**User**
- `Id` (int, PK)
- `EntraObjectId` (Guid) — unique identifier from Entra ID
- `FirstName`, `LastName`, `Email` — sourced from Entra ID during provisioning
- `Role` (UserRole enum) — Administrator, CentreManager, Staff, Client
- `LocationId` (int, FK, nullable) — centre managers and staff belong to a location; clients do not
- `IsActive` (bool) — controls access to FlexiSpace (does not disable Entra account)
- `CreatedAt` (DateTime, UTC)
- **Navigation:** One → Many Bookings, ModifiedBookings, CancelledBookings, Notifications, AuditLogs

**Location**
- `Id` (int, PK)
- `Name`, `Address` (string)
- **Navigation:** One → Many Boardrooms, Users, LocationCalendarAccounts

**Boardroom**
- `Id` (int, PK)
- `LocationId` (int, FK)
- `Name` (string)
- `Capacity` (int) — maximum number of attendees
- `Status` (BoardroomStatus enum) — Available, Maintenance, Unavailable
- `IsActive` (bool)
- `CreatedAt` (DateTime, UTC)
- **Navigation:** One → Many Bookings, BoardroomEquipments

**Booking**
- `Id` (int, PK)
- `BoardroomId`, `UserId` (int, FK)
- `BookingDate` (DateOnly), `StartTime`, `EndTime` (TimeOnly)
- `Status` (BookingStatus enum) — Pending, Cancelled, Completed
- `Company` (string, nullable), `NumberOfAttendees` (int), `Notes` (string, nullable)
- `OutlookEventId` (string, nullable) — synced to Outlook calendar if M365 available
- `CreatedAt`, `ModifiedAt` (DateTime, UTC)
- `ModifiedById`, `CancelledById` (int, FK, nullable) — tracks centre manager actions
- **Navigation:** User (booker), Boardroom, ModifiedBy/CancelledBy (users), BookingEquipments, BookingCaterings

**Equipment & Catering**
- `Id` (int, PK)
- `Name`, `Description` (string)
- `IsActive` (bool)
- `CreatedAt` (DateTime, UTC)

**BookingEquipment, BookingCatering** (join tables)
- Composite keys: (BookingId, EquipmentId) and (BookingId, CateringId)
- `Quantity` (int) — number of items requested

**Notification** (system-managed)
- `Id` (int, PK)
- `UserId` (int, FK)
- `Title`, `Message` (string)
- `Type` (NotificationType enum) — BookingCreated, BookingCancelled, BookingReminder, BookingModified
- `IsRead` (bool)
- `CreatedAt`, `SentAt` (DateTime, UTC)
- **Note:** Rows are created by services (booking approval/rejection/cancellation), not by users directly

**AuditLog** (system-managed)
- `Id` (int, PK)
- `UserId`, `Action` (AuditAction enum), `EntityName`, `EntityId` (string)
- `OldValues`, `NewValues` (JSON strings of before/after state)
- `Timestamp` (DateTime, UTC)

---

## 3. Authentication & Authorization Flow

### Token Validation
1. Client obtains JWT from Entra ID
2. Client includes token in `Authorization: Bearer <token>` header
3. API validates JWT signature against Entra ID public keys
4. Token contains claims: `oid` (object ID), `email`, `roles` (Entra app roles)

### User Resolution (ICurrentUserService)
- Maps token's `oid` claim → `User.EntraObjectId`
- Looks up user in FlexiSpace database via EF Core
- Returns `null` if user doesn't exist or `IsActive = false`
- **Scoped registration:** cached per-request to avoid repeated DB round-trips
- Used by RBAC filter and controllers to enforce permissions

### Role Hierarchy & Scoping

| Role | Permissions |
|------|-------------|
| **Administrator** | Full access: all bookings, all users, locations, equipment, catering, audit logs |
| **CentreManager** | Location-scoped: bookings for their location only, approve/reject/cancel, user management for their location |
| **Staff** | Own bookings only: create, view, cancel; cannot approve others' bookings |
| **Client** | Own bookings only; does not belong to a location |

### Authorization Enforcement
- Controllers use `[Authorize]` attribute to require valid token
- Controllers use `[AuthorizeRoles(UserRole.Administrator)]` custom attribute to restrict by role
- When an endpoint requires scoping (e.g., CentreManager sees only location bookings), the controller loads the current user and filters results

**Example:** In `UserController.GetAllUsers()`:
```c#
var currentUser = await _currentUserService.GetCurrentUserAsync();
if (currentUser.Role == UserRole.Administrator)
    filtered = users;  // See all users
else if (currentUser.Role == UserRole.CentreManager)
    filtered = users.Where(u => u.LocationId == currentUser.LocationId);  // See only your location
else
    return Forbid();  // Staff and clients cannot list users
```

---

## 4. API Endpoints Reference

All endpoints require `[Authorize]` (valid Entra ID token). Base URL: `/api/`

### Users
Endpoint behavior scoped by role (Admin > CentreManager > Staff/Client).

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/user` | Admin/CentreManager | List users (scoped by location for managers) |
| `GET` | `/user/{id}` | Admin/CentreManager/Self | Get user details |
| `POST` | `/user` | Admin | Provision Entra user into FlexiSpace |
| `PUT` | `/user/{id}` | Admin/Self | Update profile (name, location) |
| `PATCH` | `/user/{id}/status` | Admin | Activate/deactivate user |

**POST /user** — Provision from Entra
```json
{
  "entraObjectId": "guid-here",
  "locationId": 1  // optional; clients have null
}
```
Response: User object with ID assigned

**PUT /user/{id}** — Update profile
```json
{
  "firstName": "New",
  "lastName": "Name",
  "locationId": 2  // optional
}
```

**PATCH /user/{id}/status** — Activate/deactivate
```json
{
  "isActive": false
}
```

---

### Locations
Locations are **pre-seeded** with equipment and catering.

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/location` | None (public) | List all locations |
| `GET` | `/location/{id}` | None (public) | Get location with boardrooms |
| `POST` | `/location` | Admin | Create new location |
| `PUT` | `/location/{id}` | Admin | Update location |
| `POST` | `/location/calendar-accounts` | Admin | Register calendar account for location |

**GET /location** — Returns all locations with boardrooms
```json
[
  {
    "id": 1,
    "name": "Eagle Canyon",
    "address": "123 Main St",
    "boardrooms": [{ "id": 1, "name": "Board 1", ... }]
  }
]
```

**POST /location** — Create location
```json
{
  "name": "New Location",
  "address": "456 Test Ave"
}
```

---

### Boardrooms
All boardrooms include **equipment inventory**. Equipment is **not mutable per boardroom** yet (TODO).

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/boardroom` | None (public) | List all boardrooms with equipment |
| `GET` | `/boardroom/{id}` | None (public) | Get boardroom details + equipment |
| `POST` | `/boardroom` | Admin | Create boardroom + assign equipment |
| `PUT` | `/boardroom/{id}` | Admin/CentreManager | Update status/capacity |
| `DELETE` | `/boardroom/{id}` | Admin | Soft-delete boardroom |

**GET /boardroom** — Returns all boardrooms
```json
[
  {
    "id": 1,
    "name": "Board 1",
    "locationId": 1,
    "capacity": 10,
    "status": "Available",
    "equipment": [
      { "id": 1, "name": "Projector", "quantity": 1 }
    ]
  }
]
```

**POST /boardroom** — Create boardroom with equipment
```json
{
  "name": "New Boardroom",
  "capacity": 20,
  "locationId": 1,
  "status": "Available",
  "equipment": [
    { "equipmentId": 1, "quantity": 2 },
    { "equipmentId": 2, "quantity": 1 }
  ]
}
```

**TODO:** Equipment assignment per boardroom is one-way only (create). Update/reassign not yet implemented.

---

### Bookings
**Status workflow:** `Pending` → `Completed` or `Cancelled`

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/booking` | Auth | List bookings (RBAC scoped) |
| `GET` | `/booking/search` | Auth | Paginated search with filters |
| `GET` | `/booking/{id}` | Auth | Get booking details + equipment/catering |
| `POST` | `/booking` | Staff/Client | Create new booking |
| `PUT` | `/booking/{id}` | Booker/Admin | Update booking |
| `PATCH` | `/booking/{id}/approve` | CentreManager/Admin | Approve pending booking |
| `PATCH` | `/booking/{id}/reject` | CentreManager/Admin | Reject pending booking |
| `PATCH` | `/booking/{id}/cancel` | Booker/CentreManager/Admin | Cancel booking |

**GET /booking** — List (scoped by role)
```
Admin: all bookings
CentreManager: bookings for their location
Staff/Client: only own bookings
```

**GET /booking/search** — Paginated search with filters
```json
{
  "page": 1,
  "pageSize": 20,
  "locationId": 1,          // optional
  "boardroomId": 1,         // optional
  "userId": 5,              // optional
  "status": "Pending",      // optional
  "startDate": "2026-10-15",
  "endDate": "2026-10-31",
  "search": "test"          // optional: free-text on Company/Notes
}
```

**POST /booking** — Create booking
```json
{
  "boardroomId": 1,
  "bookingDate": "2026-10-15",
  "startTime": "09:00",
  "endTime": "10:00",
  "numberOfAttendees": 5,
  "company": "Test Corp",
  "notes": "Team sync",
  "equipment": [
    { "equipmentId": 1, "quantity": 1 }
  ],
  "catering": [
    { "cateringId": 1, "quantity": 10 }
  ]
}
```

**Validation on create:**
- ✅ Boardroom exists and is Active
- ✅ Time is in the future
- ✅ No overlapping bookings for same boardroom
- ✅ Attendees ≤ boardroom capacity
- ✅ Equipment/catering items exist and are active
- ❌ Approval workflow: Auto-approved? Sent to manager? → **Not yet fully specified** (see section 7)

**PUT /booking/{id}** — Update booking (date, time, equipment, catering)
```json
{
  "boardroomId": 1,
  "bookingDate": "2026-10-16",
  "startTime": "10:00",
  "endTime": "11:00",
  "numberOfAttendees": 6,
  "company": "Test Corp",
  "notes": "Updated notes",
  "equipment": [],
  "catering": []
}
```
- Cannot update if status is Completed or Cancelled

**PATCH /booking/{id}/approve** — Approve (CentreManager/Admin only)
```json
{
  "notes": "Approved by centre manager"
}
```

**PATCH /booking/{id}/cancel** — Cancel booking
```json
{
  "reason": "No longer needed"
}
```

---

### Equipment
Pre-seeded with standard items.

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/equipment` | Auth | List all equipment |
| `GET` | `/equipment/{id}` | Auth | Get equipment details |
| `POST` | `/equipment` | Admin | Create equipment |
| `PUT` | `/equipment/{id}` | Admin | Update equipment |
| `DELETE` | `/equipment/{id}` | Admin | Mark inactive |

**Example:**
```json
{
  "id": 1,
  "name": "Projector",
  "description": "4K projector for boardroom",
  "isActive": true
}
```

---

### Catering
Pre-seeded with standard menu items.

| Method | Path | Role Restriction | Purpose |
|--------|------|------------------|---------|
| `GET` | `/catering` | Auth | List all catering options |
| `GET` | `/catering/{id}` | Auth | Get catering details |
| `POST` | `/catering` | Admin | Create catering option |
| `PUT` | `/catering/{id}` | Admin | Update catering |
| `DELETE` | `/catering/{id}` | Admin | Mark inactive |

---

### Notifications
System-managed (no POST by users). Every user has an inbox scoped to themselves.

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/notification` | List current user's notifications (newest first) |
| `GET` | `/notification/unread-count` | Get unread count (for badge) |
| `PATCH` | `/notification/{id}/read` | Mark one notification as read |
| `PATCH` | `/notification/read-all` | Mark all notifications as read |

**GET /notification**
```json
[
  {
    "id": 1,
    "title": "Booking Approved",
    "type": "BookingCreated",
    "message": "Your booking for Board 1 on 2026-10-15 10:00–11:00 has been created.",
    "isRead": false,
    "createdAt": "2026-10-03T15:30:00Z"
  }
]
```

---

### AI Recommendations
Uses Groq AI API to suggest boardrooms based on meeting description.

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/ai/suggest` | Get boardroom suggestion from AI |

**POST /ai/suggest** — Suggest boardroom
```json
{
  "userNeed": "We need a room for 15 people with projector for a 2-hour meeting",
  "bookingDate": "2026-10-15",
  "startTime": "14:00",
  "endTime": "16:00"
}
```

**Response:**
```json
{
  "success": true,
  "suggestedBoardroomId": 5,
  "boardroomName": "Board 5",
  "reason": "Capacity 20 (≥15), available 14:00–16:00, has projector"
}
```

---

## 5. Key Services & Dependency Injection

All services are registered as **Scoped** in `Program.cs` and injected into controllers.

### Core Services (Ready to Use)

**IUserService** — User provisioning and management
```c#
Task<IEnumerable<User>> GetAllUsersAsync();
Task<User?> GetUserByIdAsync(int id);
Task<bool> UpdateUserAsync(int id, User user);  // name, location
Task<bool> UpdateUserStatusAsync(int id, bool isActive);
Task<User?> ProvisionUserAsync(UserProvisionDto dto);  // from Entra
```

**ILocationService** — Locations with boardrooms
```c#
Task<IEnumerable<Location>> GetAllLocationsAsync();
Task<Location?> GetLocationByIdAsync(int id);
Task<Location> CreateLocationAsync(Location location);
Task<bool> UpdateLocationAsync(int id, Location location);
Task<bool> DeleteLocationAsync(int id);
```

**IBoardroomService** — Boardrooms with equipment
```c#
Task<IEnumerable<Boardroom>> GetAllBoardroomsAsync();
Task<Boardroom?> GetBoardroomByIdAsync(int id);
Task<Boardroom> CreateBoardroomAsync(Boardroom boardroom);
Task<bool> UpdateBoardroomAsync(int id, Boardroom boardroom, List<BoardroomEquipment> equipment);
Task<bool> DeleteBoardroomAsync(int id);
```

**IBookingService** — Core booking CRUD & search
```c#
Task<IEnumerable<Booking>> GetAllBookingsAsync();
Task<Booking?> GetBookingByIdAsync(int id);
Task<Booking> CreateBookingAsync(Booking booking);
Task<bool> UpdateBookingAsync(int id, Booking booking, List<BookingEquipment> equipment, List<BookingCatering> catering);
Task<bool> DeleteBookingAsync(int id);  // soft delete: set Status = Cancelled
Task<bool> UpdateBookingStatusAsync(int id, BookingStatus status, int? approvedById);
Task<PagedResult<Booking>> SearchBookingsAsync(BookingQueryParameters query);
Task<IEnumerable<Boardroom>> GetAvailableBoardroomsAsync(DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime);
```

**INotificationService** — Notifications (system-managed)
```c#
Task<Notification> CreateNotificationAsync(int userId, string title, string message, NotificationType type);
Task<IReadOnlyList<Notification>> GetNotificationsForUserAsync(int userId);
Task<int> GetUnreadCountAsync(int userId);
Task<bool> MarkAsReadAsync(int notificationId, int userId);  // scoped to user
Task<int> MarkAllAsReadAsync(int userId);
```

**ICurrentUserService** — Resolve current caller from token
```c#
Task<User?> GetCurrentUserAsync();  // null if unauthenticated or inactive
Task<int?> GetCurrentUserIdAsync();
```

**IAiRecommendationService** — AI boardroom suggestions (Groq API)
```c#
Task<AiSuggestionResponseDto> SuggestBoardroomAsync(
    string userNeed,
    DateOnly? bookingDate,
    TimeOnly? startTime,
    TimeOnly? endTime);
```

---

### Supporting Services (Ready, but Limited)

**IAuditService** — Audit logging (infrastructure only; reporting UI not yet built)
```c#
Task LogAsync(int userId, AuditAction action, string entityName, string entityId, string? oldValues = null, string? newValues = null);
Task<IReadOnlyList<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId);
Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50);
```
- **Current state:** Logged but not yet surfaced in API/UI
- **Next phase:** Admin dashboard for viewing audit trails

**IEmailService** — Send emails via Microsoft Graph
```c#
Task SendBookingConfirmationAsync(
    string recipientEmail,
    string recipientName,
    string boardroomName,
    string locationName,
    string locationAddress,
    DateOnly bookingDate,
    TimeOnly startTime,
    TimeOnly endTime,
    int numberOfAttendees,
    string company,
    string notes);
```
- **Current state:** Implemented, but requires M365 subscription
- **Configuration:** `appsettings.json` → MicrosoftGraph:TenantId, ClientId, ClientSecret, SenderEmail

**ICalendarService** — Sync bookings to Outlook
```c#
Task<string?> CreateCalendarEventAsync(...);  // returns OutlookEventId
Task DeleteCalendarEventAsync(string eventId);
Task UpdateCalendarEventAsync(string eventId, ...);
```
- **Current state:** Implemented, but Graph calls are **commented out** in booking service (see section 7)
- **Why:** Requires M365 tenant + verified calendar mailbox

**IEntraUserService** — Retrieve users from Entra ID
```c#
Task<IEnumerable<EntraUserResponseDto>> GetUsersAsync();  // active users with app roles
Task<EntraUserResponseDto?> GetUserByIdAsync(Guid entraObjectId);
```
- Used by admin user provisioning UI
- Requires M365 tenant app registration

---

## 6. Database Setup & Seeding

### Connection String
Set in `appsettings.json` or environment variable:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=FlexiSpace;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
  }
}
```

### Migrations
EF Core migrations exist for the data model. To set up:
```bash
cd FlexiSpace-Boardroom-Booking-System/FlexiSpace
dotnet ef database update --project FlexiSpace.Infrastructure --startup-project FlexiSpace.API
```

### Seed Data
On API startup, `Program.cs` runs `DatabaseSeeder.SeedUsersAsync()`:
- Creates pre-defined users (staff, centre manager, admin) with Entra-like GUIDs
- These are for **development/testing only**

Pre-seeded entities:
- **Locations:** Eagle Canyon, Houghton, Centurion
- **Boardrooms:** 2–3 per location with varying capacities
- **Equipment:** Projector, Whiteboard, Video Conference, etc.
- **Catering:** Coffee, Lunch, Snacks, etc.

---

## 7. Known Gaps & Incomplete Features

### Graph Integration (Email & Calendar)
**Status:** Implemented but **disabled**

**Why:** Requires active M365 tenant subscription.
- Email sending is commented out in booking service
- Calendar sync is commented out in booking service
- Endpoints compile and run, but don't trigger external calls

**To enable:**
1. Set up Azure app registration with Graph API delegated/application permissions
2. Add to `appsettings.json`:
   ```json
   "MicrosoftGraph": {
     "TenantId": "...",
     "ClientId": "...",
     "ClientSecret": "...",
     "SenderEmail": "bookings@yourorg.com"
   }
   ```
3. Uncomment Graph calls in `BookingService.cs`
4. Test with `/api/graphemail/send-test` endpoint (dev only)

---

### Booking Approval Workflow
**Status:** Partially implemented

**Current behavior:**
- Booking created with `Status = Pending`
- Centre manager can approve/reject via PATCH endpoints
- **Missing:** Auto-notification to centre manager when booking is created
- **Missing:** Workflow rules (e.g., does staff always need approval? do clients?)

**To complete:**
1. Define approval rules by role in `BookingService`
2. Call `INotificationService.CreateNotificationAsync()` when booking enters approval queue
3. Add workflow status transitions (e.g., Pending → UnderReview → Approved → Completed)

---

### Reporting & Audit Dashboard
**Status:** Audit logging is implemented; reporting UI is not

**Current capability:**
- `IAuditService.GetLogsForEntityAsync(entityName, entityId)` retrieves change history
- `IAuditService.GetRecentLogsAsync(count)` gets last N actions
- Logs include old/new values (JSON) for change tracking

**To implement:**
1. Create admin dashboard controller that calls audit service
2. Build UI to filter/search audit logs by entity, date, user, action
3. Display before/after diffs for update/delete actions

---

### Boardroom Equipment Management
**Status:** One-way assignment only

**Current:** Create boardroom → assign equipment (immutable once created)  
**Missing:** Update equipment list for existing boardroom

**To complete:**
1. Modify `BoardroomService.UpdateBoardroomAsync()` to accept new equipment list
2. Implement diff logic: remove old, add new, preserve unchanged

---

### Conflict Detection & Availability
**Status:** Implemented in `BookingService`

**Current:**
- Booking creation checks for overlapping bookings in same boardroom
- `GetAvailableBoardroomsAsync()` filters boardrooms by availability window
- No special handling for maintenance windows (yet)

**To improve:**
1. Add block-room feature (centre manager can block time for maintenance/events)
2. Add hard-stop rules (e.g., no bookings < 30 min before/after maintenance)

---

## 8. How to Run the API

### Prerequisites
- .NET 8 SDK
- SQL Server (local or Azure)
- Visual Studio 2022 or VS Code + C# extension

### Run for Development
```bash
cd FlexiSpace-Boardroom-Booking-System/FlexiSpace
dotnet run --project FlexiSpace.API
```

API will start on `https://localhost:7123` (or shown in console).

### Swagger/OpenAPI
Once running, visit:
```
https://localhost:7123/swagger/ui
```

Swagger lets you:
- Browse all endpoints
- See request/response schemas
- Paste Entra ID bearer token for live testing

**To test with Swagger:**
1. Obtain JWT from Entra ID or use a test token
2. Click **Authorize** button
3. Paste token: `Bearer eyJhbGci...`
4. Try endpoints

---

### Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FlexiSpace;Integrated Security=true;"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-api-app-id",
    "Audience": "your-api-identifier"
  },
  "MicrosoftGraph": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-id",
    "ClientSecret": "your-secret",
    "SenderEmail": "noreply@yourorg.com"
  },
  "GroqAI": {
    "ApiKey": "your-groq-api-key"
  }
}
```

---

## 9. Adding New Endpoints

### Template: Following the Layered Pattern

1. **Define DTO** (if needed)
   ```c#
   // FlexiSpace.Core/DTOs/MyFeature/MyRequestDto.cs
   public class MyRequestDto
   {
       public string Name { get; set; }
       public int LocationId { get; set; }
   }
   ```

2. **Add interface method** to service
   ```c#
   // FlexiSpace.Core/Services/IMyService.cs
   public interface IMyService
   {
       Task<MyEntity> CreateAsync(MyRequestDto dto);
   }
   ```

3. **Implement service**
   ```c#
   // FlexiSpace.Infrastructure/Services/MyService.cs
   public class MyService : IMyService
   {
       private readonly ApplicationDbContext _context;
       
       public MyService(ApplicationDbContext context) => _context = context;
       
       public async Task<MyEntity> CreateAsync(MyRequestDto dto)
       {
           var entity = new MyEntity { Name = dto.Name, LocationId = dto.LocationId };
           _context.MyEntities.Add(entity);
           await _context.SaveChangesAsync();
           return entity;
       }
   }
   ```

4. **Register in Program.cs**
   ```c#
   builder.Services.AddScoped<IMyService, MyService>();
   ```

5. **Create controller**
   ```c#
   // FlexiSpace.API/Controllers/MyController.cs
   [ApiController]
   [Route("api/[controller]")]
   [Authorize]
   public class MyController : ControllerBase
   {
       private readonly IMyService _myService;
       private readonly ICurrentUserService _currentUserService;
       
       public MyController(IMyService myService, ICurrentUserService currentUserService)
       {
           _myService = myService;
           _currentUserService = currentUserService;
       }
       
       [HttpPost]
       [AuthorizeRoles(UserRole.Administrator)]
       public async Task<ActionResult<MyResponseDto>> Create([FromBody] MyRequestDto dto)
       {
           var currentUser = await _currentUserService.GetCurrentUserAsync();
           if (currentUser == null) return Unauthorized();
           
           var entity = await _myService.CreateAsync(dto);
           return CreatedAtAction(nameof(Get), new { id = entity.Id }, MapToDto(entity));
       }
   }
   ```

6. **Add audit logging** (if tracking changes)
   ```c#
   await _auditService.LogAsync(
       userId: currentUser.Id,
       action: AuditAction.Create,
       entityName: "MyEntity",
       entityId: entity.Id.ToString(),
       newValues: JsonSerializer.Serialize(entity)
   );
   ```

---

## 10. Integration Checklist for Razor Frontend

- [ ] **Auth**: Wire up Entra ID login, store JWT
- [ ] **HTTP Client**: Create reusable HttpClient with Bearer token attachment
- [ ] **Dashboard**: Call `GET /api/booking` to load user's bookings
- [ ] **Book Wizard**: Call `GET /api/location` → `GET /api/boardroom` → `POST /api/booking`
- [ ] **Approval Console**: Call `GET /api/booking/search` (centre managers) to filter and PATCH approve/reject
- [ ] **Notifications**: Poll `GET /api/notification` or set up SignalR for real-time
- [ ] **User Profile**: Call `PUT /api/user/{id}` to update name/location
- [ ] **Admin Panel**: Add user provisioning UI → `POST /api/user` with Entra lookup
- [ ] **Role-based UI**: Hide/show features per `currentUser.Role`
- [ ] **Error Handling**: Map API status codes (400, 403, 404, 409) to user-friendly messages

---

## 11. Integration Checklist for MAUI Mobile App

- [ ] **Auth**: Integrate MSAL for Entra ID mobile login (MSAL.Net)
- [ ] **HTTP Client**: Use `HttpClient` with interceptor to attach JWT
- [ ] **Booking Browser**: Call `GET /api/location` and `GET /api/boardroom`
- [ ] **Availability Check**: Call `GET /api/booking` with date filter to show free time slots
- [ ] **Create Booking**: Call `POST /api/booking` from booking form
- [ ] **My Bookings**: Call `GET /api/booking` (staff: own only; managers: location filtered)
- [ ] **Approval Flow**: Call `PATCH /api/booking/{id}/approve|reject` for managers
- [ ] **Notifications**: `GET /api/notification` + local push notifications on new status
- [ ] **Offline Support**: Cache boardrooms and bookings locally; sync on reconnect
- [ ] **Error Handling**: Map API errors to toast/snackbar messages

---

## 12. Testing with Test Data

### Test Users (Seeded)
| Email | Role | Password | Notes |
|-------|------|----------|-------|
| `admin@flexispace.com` | Administrator | (set in seed) | Full access |
| `manager@flexispace.com` | CentreManager | (set in seed) | Location-scoped |
| `staff@flexispace.com` | Staff | (set in seed) | Own bookings only |
| `client@flexispace.com` | Client | (set in seed) | Self-service booking |

### Test Endpoints
```bash
# Get all locations
curl -H "Authorization: Bearer $TOKEN" https://localhost:7123/api/location

# Get all boardrooms
curl -H "Authorization: Bearer $TOKEN" https://localhost:7123/api/boardroom

# Create a booking (staff)
curl -X POST \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "boardroomId": 1,
    "bookingDate": "2026-10-15",
    "startTime": "09:00",
    "endTime": "10:00",
    "numberOfAttendees": 5
  }' \
  https://localhost:7123/api/booking
```

---

## 13. Troubleshooting

### 401 Unauthorized
- Verify token is valid and not expired
- Check Entra ID tenant ID and audience match
- Ensure user exists in FlexiSpace DB and `IsActive = true`

### 403 Forbidden
- User doesn't have required role (e.g., staff trying to approve bookings)
- Check `[AuthorizeRoles(...)]` attribute on controller method

### 409 Conflict (Booking Creation)
- Overlapping booking already exists for that boardroom/time
- Attendees exceed boardroom capacity
- Equipment/catering item doesn't exist or is inactive

### 404 Not Found
- Boardroom, user, or booking doesn't exist
- Check IDs are integers and exist in DB

### Email/Calendar Not Sending
- M365 credentials not configured or expired
- Sender email address doesn't have mailbox permission
- Uncomment Graph calls in `BookingService.cs`

---

## 14. Next Steps & Handoff

### Phase 2: Frontend Integration
1. **Razor Team:** Implement booking wizard, approval console, audit dashboard
2. **MAUI Team:** Implement mobile booking, notifications, offline mode

### Phase 3: Advanced Features
1. Reporting & analytics (audit dashboard, booking trends)
2. Calendar sync with Outlook (uncomment Graph code + test)
3. Email notifications (uncomment Graph code + test)
4. Recurring bookings (new entity model)
5. Room blocking & maintenance windows (new entity model)

### Support & Questions
- **Database Schema:** See `ApplicationDbContext.cs`
- **Enums:** See `FlexiSpace.Core/Enums/`
- **DTOs:** See `FlexiSpace.Core/DTOs/`
- **Controllers:** See `FlexiSpace.API/Controllers/`

---

**Document Version:** 1.0  
**Last Updated:** October 2026  
**Prepared by:** Khensani Maluleke
