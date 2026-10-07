# Classroom 50 Link
https://github.com/EMGPRS/insy7315-2026-task-2-maluleke-khensani

# FlexiSpace — Boardroom Booking System

INSY7315 2026 Task 2 (WIL). Boardroom booking for FlexiSpace's Centurion, Houghton Estate, and Eagle Canyon sites.

Staff, centre managers, administrators, and clients sign in with **Microsoft Entra ID**. The Blazor web app and the MAUI mobile app both call the **ASP.NET Core Web API**, which stores data in SQL Server (LocalDB locally, Azure SQL in Azure).

## Demonstration

Watch the walkthrough of the system:

**[FlexiSpace demonstration (YouTube)](https://www.youtube.com/watch?v=yJiAFOniyEM)**

## What's in this repository

Use the **top-level** projects. Those are the API, website, and mobile app for this submission.

| Path | What it is |
|---|---|
| `FlexiSpace/` | Backend solution (`.NET 8`). Open `FlexiSpace/FlexiSpace.slnx`. |
| `FlexiSpace/FlexiSpace.API` | Controllers, JWT/Entra auth, `Program.cs`. HTTPS: `https://localhost:7055` (Swagger). |
| `FlexiSpace/FlexiSpace.Core` | Entities, DTOs, enums, service interfaces |
| `FlexiSpace/FlexiSpace.Infrastructure` | EF Core `ApplicationDbContext`, migrations, seeders, services |
| `FlexiSpace/FlexiSpace.Tests` | xUnit tests (run by GitHub Actions) |
| `Flexispace.Web/` | **Website:** Blazor Server app (`.NET 10`). Open `Flexispace.Web/Flexispace.Web.sln`. Signs in with Microsoft and calls the API. HTTPS: `https://localhost:7269`. |
| `Flexispace.Mobile/` | **Mobile:** .NET MAUI app (`.NET 10`, Windows-first). Open `Flexispace.Mobile/Flexispace.Mobile.sln`. |
| `docs/` | Client-to-API connection notes and a features/status handover |
| `DATABASE_BACKEND_HANDOVER.md` | Database, backend, auth, and Azure deployment |
| `BACKEND_HANDOVER.md` | Backend technical handover |
| `.github/workflows/dotnet-tests.yml` | CI: restore, build, and test the API on `main` |

The website is **`Flexispace.Web`** (Blazor Server). There is no React frontend in this repository.

## What the system does

- Book boardrooms at the three FlexiSpace sites (location → room → date/time → details)
- Grey out meeting times that are already booked
- Show live room availability
- Manage bookings (edit, cancel, change status in role/location scope)
- Provision Entra users into FlexiSpace, activate/deactivate accounts, and filter by role and location (Administrators)
- Reports (Administrators)
- In-app notifications
- Optional Outlook/Graph email, Azure Notification Hubs push, and Groq AI room suggestions when those settings are present

Roles come from the FlexiSpace `Users` table (Administrator, Centre Manager, Staff, Client), not from Entra app roles.

## Tech stack

- Backend: ASP.NET Core Web API (.NET 8), Entity Framework Core 8
- Database: SQL Server locally, Azure SQL in Azure
- Web: Blazor Server (.NET 10)
- Mobile: .NET MAUI (.NET 10)
- Auth: Microsoft Entra ID (JWT to the API)
- Calendar and email: Microsoft Graph (optional)
- Mobile push: Azure Notification Hubs (optional)
- AI room suggestions: Groq (optional)

Hosted API (Azure App Service):

`https://flexispace-exdye9dzg3bhejh9.southafricanorth-01.azurewebsites.net`

## Getting started

Secrets, IDs, and connection strings are **not** kept in `appsettings.json`. Each person keeps their own in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). In Azure they go in App Service settings. Do not commit them.

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

Any SQL Server works for `DefaultConnection`. In Development the API seeds the three sites, boardrooms, equipment and catering catalogues, and `SeedUsers`, adding only what is missing. Nothing is seeded outside Development.

Optional settings (the API logs a warning at startup while they are missing):

| Setting | Turns on |
|---|---|
| `MicrosoftGraph:TenantId`, `ClientId`, `ClientSecret`, `SenderEmail` | Outlook calendar sync and booking emails |
| `NotificationHubs:ConnectionString`, `HubName` | Mobile push notifications |
| `Groq:ApiKey` | AI room suggestions |

If `dotnet ef database update` fails with "DeviceTokens already exists", drop the local database and update again:

```powershell
dotnet ef database drop --project ../FlexiSpace.Infrastructure
dotnet ef database update --project ../FlexiSpace.Infrastructure
```

### 2. Web app (Blazor)

Start the API first. Then:

```powershell
cd Flexispace.Web/FlexiSpace.Web
dotnet user-secrets set "AzureAd:TenantId" "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId" "<web app registration client id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<web app client secret>"
dotnet user-secrets set "FlexiSpaceApi:Scope" "api://<API client id>/access_as_user"
dotnet run --launch-profile https
```

Use the `https` profile (`https://localhost:7269`). Plain `dotnet run` picks the http profile, whose redirect URL Entra rejects (`AADSTS50011`). Screens and routes are listed in `Flexispace.Web/README.md`.

### 3. Mobile app (MAUI)

Windows-first. See `Flexispace.Mobile/README.md` for the MAUI workload, restore, and run commands. The app project is `Flexispace.Mobile/Flexispace.Mobile`. Start the API first (`https://localhost:7055`).

## Tests and CI

```powershell
dotnet test FlexiSpace/FlexiSpace.Tests
```

GitHub Actions (`.github/workflows/dotnet-tests.yml`) restores and builds `FlexiSpace/FlexiSpace.slnx` and runs those tests on every push and pull request to `main`.

## More documentation

| File | Topic |
|---|---|
| `Flexispace.Web/README.md` | Web screens, routes, and how to run Blazor |
| `Flexispace.Mobile/README.md` | MAUI setup, sign-in, and project layout |
| `Flexispace.Mobile/Flexispace.Mobile/API_CONTRACT.md` | HTTP API the mobile app expects |
| `docs/Connect_Client_To_API.md` | How web and mobile authenticate and call the API |
| `docs/Handover_Features_and_Status.md` | Implemented API features and status |
| `DATABASE_BACKEND_HANDOVER.md` | Schema, seed data, Azure |
| `BACKEND_HANDOVER.md` | Backend architecture handover |

## Team

| Member | Role |
|---|---|
| Khensani | DB schema, EF Core, Entra ID auth, Graph/Outlook sync |
| Tino | Booking CRUD, conflict detection, search/filter |
| Denzel | RBAC, admin/user mgmt, audit trail, reporting, front-end/API integration |
| Khumo | Website UI (Blazor web app) |
| Riba | Mobile UI (MAUI) |
