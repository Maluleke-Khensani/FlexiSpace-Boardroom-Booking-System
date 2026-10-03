# FlexiSpace - Boardroom Booking System

INSY7315 WIL Project — boardroom booking system for Eagle Canyon, 
Houghton, and Centurion.

## Tech Stack
- Frontend: React + Microsoft Fluent UI
- Web prototype: Blazor Server (`Flexispace.Web`)
- Backend: .NET 8 Web API
- Database: Microsoft SQL Server (Azure SQL) + EF Core
- Mobile: .NET MAUI
- Auth: Microsoft Entra ID

## Web prototype (Blazor)
Khumo-Thato Chabeli (ST10448834) — browser-based booking UI prototype using mock data.

```powershell
cd FlexiSpace
dotnet run --project Flexispace.Web/Flexispace.Web.csproj
```

See `Flexispace.Web/README.md` for demo accounts and routes.

## Team
| Member | Role |
|---|---|
| Khensani | DB schema, EF Core, Entra ID auth, Graph/Outlook sync |
| Tino | Booking CRUD, conflict detection, search/filter |
| Denzel | RBAC, admin/user mgmt, audit trail, reporting |
| Khumo | Website UI (React + Fluent UI) |
| Riba | Mobile UI (MAUI) |

## Getting Started
[instructions once the project is buildable]
