# Flexispace Web

**Prototype web application** — completed by **Khumo-Thato Chabeli (ST10448834)**

This project is the browser-based counterpart to the Flexispace mobile app. It demonstrates how staff, centre managers, administrators, and clients can manage meeting-room bookings through a professional web interface aligned with the [Flexispace brand](https://flexispace.net.za).

## What it does

Flexispace Web lets users:

- **Browse and book boardrooms** across Centurion, Houghton, and Eagle Canyon via a four-step wizard (location → room → date/time → details)
- **View live room availability** with location filters and map/list layouts
- **Manage bookings** — approve or decline pending requests (Centre Manager / Admin roles)
- **Track personal bookings** and open booking detail pages for edit, cancel, or approval actions
- **Receive alerts** for confirmations, reminders, and cancellations
- **Switch demo roles** from the profile page to preview different permission levels

The app uses **mock data and services** (no live API). It is intended as a functional prototype for demonstration and assessment purposes.

## Tech stack

- **ASP.NET Core Blazor Server** (interactive server render mode)
- **Flexispace.Core** — shared models, helpers, and mock services
- **CommunityToolkit.Mvvm** — ViewModels with RelayCommand
- Custom CSS design system (`wwwroot/flexispace.css`) matching Flexispace branding

## How to run

From the solution root:

```powershell
dotnet run --project Flexispace.Web
```

Then open the URL shown in the terminal (typically `http://localhost:5282`).

## Demo accounts

Password for all accounts: **`demo123`**

| Email | Role |
|-------|------|
| `staff@flexispace.net.za` | Staff — book and cancel own bookings |
| `rebecca@flexispace.net.za` | Centre Manager — approve, block, edit |
| `admin@flexispace.net.za` | Administrator — full scope |
| `client@example.com` | Client — self-service booking |

## Project structure

| Folder | Purpose |
|--------|---------|
| `Components/Pages/` | Routable Blazor pages (one per app screen) |
| `Components/Shared/` | Reusable UI (room map, choice grids, modals) |
| `Components/Layout/` | App shell with sidebar navigation |
| `ViewModels/` | Page logic and state (MVVM pattern) |
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
| `/notifications` | Alerts |
| `/profile` | User profile |
| `/my-bookings` | Booking history |
| `/locations` | All locations |
| `/locations/{id}` | Location detail |
| `/bookings/{id}` | Booking detail |
| `/bookings/{id}/confirmation` | Booking confirmation |
