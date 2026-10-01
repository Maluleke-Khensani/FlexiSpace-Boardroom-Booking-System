# FlexiSpace - Boardroom Booking System

INSY7315 WIL Project — boardroom booking system for FlexiSpace's Centurion, Houghton Estate and Eagle Canyon sites.

## What's in this repo

| Folder | What it is | Open with |
|---|---|---|
| `FlexiSpace/` | Backend: ASP.NET Core Web API (.NET 8), EF Core, SQL Server | `FlexiSpace/FlexiSpace.slnx` |
| `FlexiSpace/FlexiSpace.API` | Controllers, authorization, `Program.cs` | |
| `FlexiSpace/FlexiSpace.Core` | Entities, DTOs, enums, service interfaces | |
| `FlexiSpace/FlexiSpace.Infrastructure` | `ApplicationDbContext`, migrations, seeders, services | |
| `FlexiSpace/FlexiSpace.Tests` | xUnit tests for the backend services (run by GitHub Actions) | |
| `Flexispace.Web/` | Blazor Server web app (.NET 10); signs in with Microsoft and calls the API | `Flexispace.Web/Flexispace.Web.sln` |
| `Flexispace.Mobile/` | .NET MAUI app (.NET 10, Windows-first) | `Flexispace.Mobile/Flexispace.Mobile.sln` |
| `FlexiSpace.TestClient/` | Small React + MSAL page for testing API sign-in | `npm install`, then `npm run dev` |
| `DATABASE_BACKEND_HANDOVER.md` | How the database, backend, auth and Azure deployment work | |

## Tech stack

- Backend: ASP.NET Core Web API (.NET 8), Entity Framework Core 8
- Database: SQL Server locally, Azure SQL in Azure
- Web: Blazor Server (.NET 10)
- Mobile: .NET MAUI (.NET 10)
- Auth: Microsoft Entra ID; app roles come from the `Users` table
- Calendar and email: Microsoft Graph
- Mobile push: Azure Notification Hubs
- AI room suggestions: Groq

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

Any SQL Server works for `DefaultConnection`: LocalDB as above, or `Server=localhost;...` as in the backend handover. In Development the API seeds the three sites, their boardrooms, the equipment and catering catalogues and the `SeedUsers`, adding only what's missing. Nothing is seeded outside Development.

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

See `Flexispace.Mobile/README.md`.

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
| Khumo | Website UI (Blazor web app) |
| Riba | Mobile UI (MAUI) |
