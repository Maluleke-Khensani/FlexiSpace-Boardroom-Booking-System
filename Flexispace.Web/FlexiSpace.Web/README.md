# Flexispace Web

Browser app for FlexiSpace, built by **Khumo-Thato Chabeli (ST10448834)**.

This is the **Blazor Server** website. It is not a React project. Staff, centre managers, administrators, and clients sign in with Microsoft Entra ID, and the app calls the FlexiSpace API.

## What it does

Flexispace Web lets users:

- **Browse and book boardrooms** across Centurion, Houghton, and Eagle Canyon via a four-step wizard (location → room → date/time → details)
- **See already-booked meeting times greyed out** on the time grid
- **View live room availability** with location filters and map/list layouts
- **Manage bookings** — edit, cancel, or change status in scope (Centre Manager / Admin)
- **Track personal bookings** and open booking detail pages
- **Provision and manage users** (Administrator) from the Users tab
- **View reports** (Administrator)
- **Receive alerts** for confirmations, reminders, and cancellations

## Tech stack

- **ASP.NET Core Blazor Server** (.NET 10, interactive server render mode)
- **Microsoft Entra ID** (OpenID Connect)
- **FlexiSpace HTTP API** for rooms, bookings, users, and occupancy
- **CommunityToolkit.Mvvm** — ViewModels with RelayCommand
- Custom CSS design system (`wwwroot/flexispace.css`) matching Flexispace branding

Open `Flexispace.Web.sln` to work on the web client. Start the API before running this app.

## How to run

From this folder (`Flexispace.Web`):

```powershell
cd FlexiSpace.Web
dotnet user-secrets set "AzureAd:TenantId" "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId" "<web app registration client id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<web app client secret>"
dotnet user-secrets set "FlexiSpaceApi:Scope" "api://<API client id>/access_as_user"
dotnet run --launch-profile https
```

Use the `https` profile: plain `dotnet run` picks the http profile, whose redirect URL Entra rejects (AADSTS50011).

Then open the HTTPS URL shown in the terminal.

## Project structure

| Folder | Purpose |
|--------|---------|
| `Components/Pages/` | Routable Blazor pages (one per app screen) |
| `Components/Shared/` | Reusable UI (room map, choice grids, modals) |
| `Components/Layout/` | App shell with sidebar navigation |
| `ViewModels/` | Page logic and state (MVVM pattern) |
| `Api/` | HTTP client for the FlexiSpace API |
| `Services/` | Web-specific navigation and auth UI bridge |
| `Helpers/` | Display formatting utilities |
| `wwwroot/` | CSS, JavaScript, and location/room images |

## Routes

| Route | Screen |
|-------|--------|
| `/` | Welcome / landing |
| `/login` | Sign in |
| `/home` | Dashboard |
| `/book` | Book a room wizard |
| `/live` | Live availability |
| `/manage` | Manage console |
| `/users` | Users (Administrator) |
| `/reports` | Reports (Administrator) |
| `/notifications` | Alerts |
| `/profile` | User profile |
| `/my-bookings` | Booking history |
| `/locations` | All locations |
| `/locations/{id}` | Location detail |
| `/bookings/{id}` | Booking detail |
| `/bookings/{id}/confirmation` | Booking confirmation |