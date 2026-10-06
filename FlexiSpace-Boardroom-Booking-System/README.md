# FlexiSpace - Boardroom Booking System

INSY7315 WIL Project — boardroom booking system for Eagle Canyon,
Houghton, and Centurion.

## Tech Stack
- Frontend: Blazor Server (`Flexispace.Web`) — this is the website, not a React app
- Backend: .NET 8 Web API
- Database: Microsoft SQL Server (Azure SQL) + EF Core
- Mobile: .NET MAUI
- Auth: Microsoft Entra ID

## Web app (Blazor)
Khumo-Thato Chabeli (ST10448834) — browser-based booking UI. Sign in with Microsoft Entra ID; the app calls the FlexiSpace API.

The canonical project is at the repo root: `Flexispace.Web`.

```powershell
cd Flexispace.Web
dotnet run --project FlexiSpace.Web --launch-profile https
```

Start the API first. Use the `https` launch profile so Entra accepts the redirect URL. See `Flexispace.Web/README.md` for screens and routes.

## Team
| Member | Role |
|---|---|
| Khensani | DB schema, EF Core, Entra ID auth, Graph/Outlook sync |
| Tino | Booking CRUD, conflict detection, search/filter |
| Denzel | RBAC, admin/user mgmt, audit trail, reporting |
| Khumo | Website UI (Blazor web app) |
| Riba | Mobile UI (MAUI) |

## Getting Started
See the root `README.md` for API, web, and mobile setup.