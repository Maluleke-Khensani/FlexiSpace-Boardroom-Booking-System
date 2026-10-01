# Flexispace Web

**Web application** — UI designed and built by **Khumo-Thato Chabeli (ST10448834)**; connected to the FlexiSpace API by Denzel.

This is the browser-based counterpart to the Flexispace mobile app. Staff, centre managers, administrators and clients sign in with their Microsoft (Entra ID) account and manage boardroom bookings through an interface aligned with the [Flexispace brand](https://flexispace.net.za).

## What it does

- **Browse and book boardrooms** across Centurion, Houghton Estate and Eagle Canyon with a four-step wizard (location → room → date/time → details)
- **View rooms by location** in map and list layouts
- **Track bookings** and open a booking to edit or cancel it
- **Receive alerts** for confirmations, reminders and cancellations
- **Manage console** for centre managers and administrators

All data comes from the FlexiSpace API (`FlexiSpace/FlexiSpace.API`); what a user can see and do depends on their role in the API's `Users` table.

## Tech stack

- **ASP.NET Core Blazor Server** (.NET 10, interactive server render mode)
- **Microsoft.Identity.Web** for Microsoft sign-in and calling the API
- **FlexiSpace.Prototype.Core** — shared models, interfaces and helpers for this app
- **CommunityToolkit.Mvvm** — view models with `RelayCommand`
- Custom CSS design system (`wwwroot/flexispace.css`) matching Flexispace branding

## How to run

1. Start the API first (see the root `README.md`).
2. Set this app's user secrets (Entra tenant, client ID, client secret and the API scope); the commands are in the root `README.md`.
3. From `Flexispace.Web/FlexiSpace.Web`:

```powershell
dotnet run --launch-profile https
```

Or open `Flexispace.Web.sln` in Visual Studio and run the `https` profile. Then open `https://localhost:7269` (or the URL shown in the terminal).

## Project structure

| Folder | Purpose |
|--------|---------|
| `FlexiSpace.Web/Components/Pages/` | Routable Blazor pages (one per app screen) |
| `FlexiSpace.Web/Components/Shared/` | Reusable UI (room map, choice grids, modals) |
| `FlexiSpace.Web/Components/Layout/` | App shell with sidebar navigation |
| `FlexiSpace.Web/ViewModels/` | Page logic and state (MVVM) |
| `FlexiSpace.Web/Services/Real/` | API client, token handling and the services that call the API |
| `FlexiSpace.Web/Services/` | Navigation, privacy consent and auth-state helpers |
| `FlexiSpace.Web/Helpers/` | Display formatting utilities |
| `FlexiSpace.Web/wwwroot/` | CSS, JavaScript, and location/room images |
| `FlexiSpace.Prototype.Core/` | Models, service interfaces and helpers shared by the pages |

## Routes

| Route | Screen |
|-------|--------|
| `/` | Welcome / landing |
| `/login` | Sign in |
| `/home` | Dashboard |
| `/book` | Book a room wizard |
| `/live` | Rooms by location |
| `/manage` | Manage console |
| `/notifications` | Alerts |
| `/profile` | User profile |
| `/my-bookings` | Booking history |
| `/locations` | All locations |
| `/locations/{id}` | Location detail |
| `/bookings/{id}` | Booking detail |
| `/bookings/{id}/confirmation` | Booking confirmation |
