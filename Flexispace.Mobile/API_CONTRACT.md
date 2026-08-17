# Flexispace Mobile — Backend API Contract

This document is the contract the mobile app expects from the .NET 8 Web API.
Today the app uses **mock services** (`Services/Mock/*`). Swap them in `MauiProgram.cs` for HTTP implementations without rewriting the UI.

Base URL (example): `https://api.flexispace.net.za/api/v1`

Auth: Microsoft Entra ID / Bearer token (mock login until Entra is wired).

---

## Auth

| Method | Path | Notes |
|--------|------|--------|
| POST | `/auth/login` | Body: `{ email, password }` → `{ token, user }` |
| POST | `/auth/logout` | Invalidate session |
| GET | `/auth/me` | Current user profile |

**User:** `{ id, name, email, role, locationId }`  
**Roles:** `Staff` · `CentreManager` · `Administrator` · `Client`

---

## Locations & rooms

| Method | Path | Notes |
|--------|------|--------|
| GET | `/locations` | Centurion, Houghton, Eagle Canyon |
| GET | `/locations/{id}` | Single location |
| GET | `/rooms?locationId=` | Optional filter |
| GET | `/rooms/{id}` | Single boardroom |
| GET | `/rooms/availability?locationId=&date=` | Status per room for day |

**Room status:** `Available` · `Occupied` · `Reserved` · `Cleaning` · `Maintenance` · `Blocked`

---

## Bookings

| Method | Path | Notes |
|--------|------|--------|
| GET | `/bookings?locationId=&userId=&day=` | Filters optional |
| GET | `/bookings/{id}` | Detail |
| GET | `/bookings/today` | Dashboard list |
| POST | `/bookings` | Create (conflict-checked) |
| DELETE | `/bookings/{id}` | Cancel (or PATCH status) |

**Create body:**
```json
{
  "roomId": "hou-exec",
  "start": "2026-07-24T10:00:00",
  "end": "2026-07-24T11:00:00",
  "company": "ACIS",
  "attendees": 6,
  "equipment": ["TV", "Video conferencing"],
  "catering": ["Coffee", "Water"],
  "notes": ""
}
```

**Create response (409 on conflict):**
```json
{
  "success": false,
  "message": "Conflict",
  "suggestedSlots": ["2026-07-24T08:00:00", "2026-07-24T12:00:00"]
}
```

Outlook: API should write `outlookEventId` via Microsoft Graph and keep two-way sync (webhooks preferred).

---

## Notifications

| Method | Path | Notes |
|--------|------|--------|
| GET | `/notifications` | Newest first |
| GET | `/notifications/unread-count` | Badge |
| POST | `/notifications/{id}/read` | Mark one |
| POST | `/notifications/read-all` | Mark all |

Types used by UI: `Confirmation` · `Reminder` · `Cancellation` · `Info`

Reminders: 24h and 1h before start (server-side job / queue).

---

## Role access (FlexiTech project plan)

| Capability | Staff | Client | Centre Manager | Administrator |
|------------|-------|--------|----------------|---------------|
| Book rooms | yes | yes | yes (own site) | yes |
| Cancel own bookings | yes | yes | yes | yes |
| View live availability | yes | — | yes (own site) | yes |
| Approve / edit / cancel any (in scope) | — | — | yes | yes |
| Block rooms | — | — | yes | yes |
| Add/remove rooms · users · reports | — | — | — | yes |
| Online payment | — | Phase 2 | — | — |

Staff/Client bookings are created as **Pending** and appear for Centre Manager approval.

Per the team's project plan, mobile only builds UI for the rows above through "Approve / edit /
cancel any (in scope)" — block rooms and add/remove rooms · users · reports are Administrator
capabilities the **backend** still needs to expose, but their UI lives on the React website's
admin dashboard, not in this app. The mobile tab bar reflects this: a role only sees the tabs
it has UI for (see `AppShell.xaml.cs`).


1. Implement `HttpAuthService`, `HttpRoomService`, `HttpBookingService`, `HttpNotificationService`.
2. In `MauiProgram.cs`, replace:
   ```csharp
   builder.Services.AddSingleton<IAuthService, MockAuthService>();
   ```
   with the HTTP versions.
3. Keep the same interfaces — ViewModels stay unchanged.

## Demo accounts (mock only)

| Email | Password | Role |
|-------|----------|------|
| staff@flexispace.net.za | demo123 | Staff |
| rebecca@flexispace.net.za | demo123 | Centre Manager |
| antoinette@flexispace.net.za | demo123 | Centre Manager |
| admin@flexispace.net.za | demo123 | Administrator |
