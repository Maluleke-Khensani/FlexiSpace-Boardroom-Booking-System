# FlexiSpace - Boardroom Booking System

INSY7315 WIL Project: one booking system for FlexiSpace's **Centurion**, **Houghton Estate** and **Eagle Canyon** sites. It stops double bookings, syncs bookings to each centre's Outlook calendar and needs almost no IT upkeep, because FlexiSpace has no internal IT team.

Three apps share one secure API:

| App | Built with | Who uses it |
|---|---|---|
| REST API | ASP.NET Core Web API (.NET 8), EF Core 8, SQL Server / Azure SQL | Everything below |
| Web app | Blazor Server (.NET 10) | Staff, Centre Managers, Administrators, Clients |
| Mobile app | .NET MAUI (.NET 10, Windows build for now) | Staff, Centre Managers, Administrators, Clients |

## Contents

- [Architecture](#architecture)
- [Front end](#front-end)
- [Back end](#back-end)
- [Hosting](#hosting)
- [GitHub workflow and CI](#github-workflow-and-ci)
- [Getting started](#getting-started)
- [Team](#team)

## Architecture

Service-oriented, layered design. The web and mobile apps hold no booking or permission rules of their own; they call one REST API, so every rule is enforced in one place.

```
 Web app (Blazor)      Mobile app (.NET MAUI)            Microsoft Entra ID
        \                    /                    (sign-in, OAuth 2.0 tokens)
         HTTPS + access token                                  |
                  |                                            |
        FlexiSpace REST API  (ASP.NET Core, .NET 8)  <-- validates tokens
        Bookings & conflicts | Rooms & locations | Users & roles (RBAC)
        Notifications & reminders | Audit trail & reports | AI suggestions
           |                |                    |                 |
     Azure SQL DB     Microsoft Graph     Notification Hubs     Groq AI
     (EF Core)        (Outlook calendar   (mobile push,         (room
                       + emails)           optional)             suggestions)
```

| Folder | What it is | Open with |
|---|---|---|
| `FlexiSpace/FlexiSpace.API` | Controllers, authorization filter, `Program.cs` | `FlexiSpace/FlexiSpace.slnx` |
| `FlexiSpace/FlexiSpace.Core` | Entities, DTOs, enums, service interfaces | |
| `FlexiSpace/FlexiSpace.Infrastructure` | `ApplicationDbContext`, EF configurations, migrations, seeders, services | |
| `FlexiSpace/FlexiSpace.Tests` | xUnit tests for the backend services (run by GitHub Actions) | |
| `Flexispace.Web/` | Blazor Server web app | `Flexispace.Web/Flexispace.Web.sln` |
| `Flexispace.Mobile/` | .NET MAUI app | `Flexispace.Mobile/Flexispace.Mobile.sln` |
| `FlexiSpace.TestClient/` | Small React + MSAL page used early on to test API sign-in | `npm install`, then `npm run dev` |
| `DATABASE_BACKEND_HANDOVER.md` | Database, backend, auth and Azure deployment in detail | |

**Why this design:** each concern is its own service behind an interface (`IBookingService`, `INotificationService`, `IEmailService`, `ICalendarService`, â€¦). An outside maintainer can change one part without touching the rest. If email or push isn't configured, a stand-in service takes over and bookings keep working.

## Front end

### Visual design and branding
- Both apps use FlexiSpace's own look: the FlexiSpace logo, a black, ivory and gold palette and the "You run your business â€” we run the rest" voice, matching flexispace.net.za.
- Shared styles keep screens consistent: `flexispace.css` (web) and `Resources/Styles` (mobile).
- Real photos of each centre and boardroom, with a fallback photo for rooms added later (`LocationPresentation.cs` in both apps).

### UX and feedback
- **Role-based navigation:** each role only sees the tabs it can use. For example, Staff get Home, Book, Live, Alerts and Profile; Centre Managers and Administrators also get Manage and Reports.
- **Step-by-step booking:** location â†’ room â†’ date and time â†’ attendees and extras, with a summary before confirming.
- **Clear feedback:** confirmation screens, in-app alerts with an unread badge, and plain-language error messages, e.g. "Boardroom A is already booked for an overlapping time".
- **Sign-in messages:** sign-in tells the user exactly what went wrong: window closed, account not set up in FlexiSpace, API unreachable, or a Microsoft error.

### Responsiveness and accessibility
- The web app's CSS adapts the layout across desktop, tablet and phone widths using media queries.
- The mobile app uses a phone-sized layout; on Windows it opens as a 400 Ã— 860 window.
- High-contrast navy/black and gold palette, labelled icons and buttons, and ARIA labels and roles on key web components.
- Privacy (PoPIA) policy and consent bar in both apps.

## Back end

### Programming
- **Layered solution:** `Core` (entities, DTOs, interfaces), `Infrastructure` (EF Core and services), `API` (controllers), with dependency injection throughout.
- **Patterns:**
  - service layer, with interfaces for every external dependency
  - a custom authorization filter attribute (`[AuthorizeRoles]`)
  - an auditable controller base class
  - null-object fallbacks (`NullEmailService`, `NullCalendarService`)
  - a background hosted service for reminders
- **Error handling:** domain exceptions (`BusinessRuleException`, `ForbiddenException`, `NotFoundException`) map to the right HTTP status codes. Side effects (Outlook, email, push) are logged, never allowed to undo a successful booking.

### Database
- SQL Server locally, Azure SQL in Azure, via EF Core code-first migrations.
- **Tables:** Locations, Boardrooms, BoardroomComponents (linked rooms), Equipment, BoardroomEquipment, Catering, Bookings, BookingEquipment, BookingCatering, BlockedPeriods, Users, LocationCalendarAccounts, Notifications, DeviceTokens, AuditLogs.
- Many-to-many relationships resolved with junction tables, which keeps the schema in 3NF.
- **Integrity:**
  - unique indexes on `Users.Email`, `Users.EntraObjectId` and `DeviceTokens.Token`
  - an index on `BlockedPeriods (BoardroomId, Start, End)` for fast conflict checks
  - `Restrict` deletes so booking history can't be wiped by deleting a room or user
  - maximum lengths on text columns
  - statuses stored as readable strings
- Every entity has its own configuration class under `Persistence/Configurations`.

### APIs

All endpoints need a Microsoft Entra access token. Role rules are enforced on the server.

| Endpoint | Methods | Purpose | Who |
|---|---|---|---|
| `/api/booking` | GET, POST, PUT, DELETE | Bookings; DELETE cancels and keeps the history | Owner; Centre Manager (own location); Admin |
| `/api/booking/search` | GET | Filtered, paged search (`items`, `totalCount`, `page`, `pageSize`) | Results scoped to the caller's role |
| `/api/booking/{id}/status` | PATCH | Change status (Confirmed â†’ Cancelled / Completed) | Owner; Centre Manager (own location); Admin |
| `/api/location`, `/api/boardroom` | GET, POST, PUT, DELETE | Centres and rooms, including linked rooms (`PUT {id}/components`) | Read: all; change: Admin (Centre Managers can also add rooms) |
| `/api/blockedperiod` | GET, POST, DELETE | Block a room for maintenance or events | Centre Manager, Admin |
| `/api/equipment`, `/api/catering` | GET, POST, PUT, DELETE | Catalogues for booking extras | Read: all; add: Admin, Centre Manager; edit/delete: Admin |
| `/api/user/me` | GET | Who is signed in and their role | Any signed-in user |
| `/api/user/me/sign-in`, `/sign-out` | POST | Login history for the audit log (`?client=web\|mobile`) | Any signed-in user |
| `/api/user` | GET, POST, PUT, PATCH | User management and provisioning | Admin |
| `/api/notification` | GET, PATCH | In-app alerts and unread count | Own alerts only |
| `/api/reporting/booking-stats`, `/bookings/export` | GET | Booking statistics and CSV export | Centre Manager (own location), Admin |
| `/api/auditlog/recent`, `/entity/{name}/{id}` | GET | Audit trail and record history | Admin |
| `/api/ai/suggest` | POST | AI room suggestions (Groq) | Any signed-in user |

**Status codes:** 200/201 on success, 204 for no content, 400 with a list of rule errors, 401 when not signed in, 403 when the role isn't allowed, 404 when not found.

**External services:**
- **Microsoft Graph:** Outlook calendar events and HTML emails.
- **Azure Notification Hubs:** mobile push. Optional; push is skipped if it isn't configured.
- **Groq:** AI room suggestions.

### Security
- **Authentication:** Microsoft Entra ID single sign-on with OAuth 2.0 tokens. Three separate app registrations: API, Web and Mobile.
  - Web: Microsoft.Identity.Web, with tokens tied to each user's session.
  - Mobile: MSAL through the Windows account broker (WAM).
- **Authorization:** roles come from the FlexiSpace `Users` table, not the token, and are checked on every request by `[AuthorizeRoles]`.
  - Data is scoped on the server: Staff only see their own bookings, and Centre Managers only see their location.
  - Least privilege: each role only gets what its job needs.
- **Input validation on the server:** times, past dates (in South African time), capacity, room status, equipment and catering are all re-checked even if a client is bypassed.
- **Secrets:** no secrets in the repo. They're kept in .NET user secrets locally and in App Service settings in Azure. The API refuses to start without a connection string.
- **Safe output:** email content is HTML-encoded. CSV exports escape cells that start with `=`, `+`, `-` or `@` (formula injection).
- **Accountability:** every create, update and cancel, and every sign-in and sign-out, is written to the audit log with who, what, when and the old and new values.

### Data flow and business logic

What happens when someone presses **Book**:

1. **Validate:** end after start, not in the past, attendees within capacity, room active and available, extras valid.
2. **Check blocked periods** on the room and any room linked to it.
3. **Check overlapping confirmed bookings,** including linked rooms. At Eagle Canyon, booking Thingamajik + Whachamacallit blocks both halves, and booking one half blocks the combined room.
4. **Save in a Serializable transaction,** so two people can't grab the same slot at once.
5. **Sync and notify:**
   - create the event in the centre's Outlook calendar and store its ID, so edits and cancellations update or remove it
   - email the booker an HTML confirmation, and email the location's Centre Managers
   - add in-app alerts

**Reminders:** a background service checks every 5 minutes.

| Before the meeting | In-app | Email |
|---|---|---|
| 24 hours | Yes | Yes, unless the booking starts within 2 hours |
| 2 hours | Yes | No |
| 1 hour | Yes | Yes |

Rescheduling resets the reminders. Cancelling never deletes a booking, so history and reports stay complete.

**Reports & audit (web, `/reports`):**
- **Booking statistics** with CSV download. Centre Managers see their own location; Administrators see all locations.
- **Audit log** (Administrators only): search, a field-by-field before/after view, and the full history of any record.

## Hosting

| Part | Azure service | Why |
|---|---|---|
| API + web app | Azure App Service: one Linux B1 plan hosts both | Azure bills per plan, not per app, and Linux is about 76% cheaper than Windows. Basic tier keeps "Always On" so the reminder service keeps running. |
| Database | Azure SQL Database (Basic) | Managed SQL Server: backups and patching handled by Azure, and the same EF Core provider as local development. |
| Sign-in | Microsoft Entra ID | FlexiSpace already uses Microsoft 365. |
| Calendar & email | Microsoft Graph | Uses FlexiSpace's existing Outlook mailboxes. |

This follows the client's Azure-only architecture. The estimated running cost is about **R300 a month** excluding VAT, with everything else on free tiers. Settings go in App Service configuration (see `DATABASE_BACKEND_HANDOVER.md`, sections 17â€“18).

**Live URLs:** *(add the API and web app URLs once deployed)*

## GitHub workflow and CI

- **Branching:**
  - every piece of work gets its own branch, e.g. `feature/entra-auth-rbac`, `feature/rbac-audit-reporting`, `feature/flexispace-mobile`, `feature/frontend-integration`
  - branches are merged into `main` through pull requests reviewed by a teammate
  - nothing is committed straight to `main`
- **CI:** `.github/workflows/dotnet-tests.yml` runs on every push and pull request to `main`. It restores, builds the .NET 8 backend and runs the xUnit test suite (105 tests). Test classes cover:
  - bookings and conflicts
  - blocked periods
  - notifications
  - auditing
  - current-user resolution
- **Next step:** a deploy job that publishes to Azure App Service once the tests pass.

## Getting started

Secrets, IDs and connection strings are **not** kept in `appsettings.json`. Each person keeps their own in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets); in Azure they go in App Service settings. Ask Khensani or Denzel for the Entra values privately. Don't paste them in the group chat or commit them.

### 1. API

```powershell
cd FlexiSpace/FlexiSpace.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=FlexiSpaceDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "AzureAd:TenantId" "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId" "<API app registration client id>"

# At least one Administrator, so someone can provision everyone else.
# Your object ID is the "oid" claim in your access token.
dotnet user-secrets set "SeedUsers:0:EntraObjectId" "<your object id>"
dotnet user-secrets set "SeedUsers:0:FirstName" "<first name>"
dotnet user-secrets set "SeedUsers:0:LastName" "<last name>"
dotnet user-secrets set "SeedUsers:0:Email" "<your sign-in email>"
dotnet user-secrets set "SeedUsers:0:Role" "Administrator"

dotnet ef database update --project ../FlexiSpace.Infrastructure
dotnet run --launch-profile https
```

Any SQL Server works for `DefaultConnection`. In Development the API seeds:
- the three sites and their boardrooms
- the equipment and catering catalogues
- the `SeedUsers`, but only while the Users table is empty

Nothing is seeded outside Development.

Optional settings (the API logs a warning at startup while they're missing):

| Setting | Turns on |
|---|---|
| `MicrosoftGraph:TenantId`, `ClientId`, `ClientSecret`, `SenderEmail` | Outlook calendar sync and booking emails |
| `NotificationHubs:ConnectionString`, `HubName` | Mobile push notifications |
| `Groq:ApiKey` | AI room suggestions |

If `dotnet ef database update` fails with "DeviceTokens already exists", your local database has an old migration applied. Run `dotnet ef database drop --project ../FlexiSpace.Infrastructure`, then update again.

### 2. Web app

```powershell
cd Flexispace.Web/FlexiSpace.Web
dotnet user-secrets set "AzureAd:TenantId" "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId" "<web app registration client id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<web app client secret>"
dotnet user-secrets set "FlexiSpaceApi:Scope" "api://<API client id>/access_as_user"
dotnet run --launch-profile https
```

Start the API first. Use the `https` profile: plain `dotnet run` picks the http profile, whose redirect URL Entra rejects (AADSTS50011). See `Flexispace.Web/README.md` for the screens and routes.

### 3. Mobile app

Windows 10/11 with the .NET 10 SDK and the MAUI workload (`dotnet workload install maui`). Start the API first, then:

```powershell
dotnet run --project Flexispace.Mobile/Flexispace.Mobile -f net10.0-windows10.0.19041.0
```

Sign-in uses the **Mobile** app registration through the Windows account broker. See `Flexispace.Mobile/README.md` for the one-time Azure setup.

### Tests

```powershell
dotnet test FlexiSpace/FlexiSpace.Tests
```

## Working on the repo

- Branch from `main`, keep pull requests small, and say in the PR how you tested it.
- Database changes (entities, configurations, migrations) go past Khensani before they're merged.
- `dotnet test FlexiSpace/FlexiSpace.Tests` must pass; GitHub Actions runs it on every pull request to `main`.

## Team

| Member | Role |
|---|---|
| Khensani | DB schema, EF Core, Entra ID auth, Graph/Outlook sync |
| Tino | Booking CRUD, conflict detection, search/filter |
| Denzel | RBAC, admin/user mgmt, audit trail, reporting, front-end/API integration |
| Kim | Data schema & security documentation |
| Khumo | Website UI (Blazor web app) |
| Riba | Mobile UI (MAUI) |
