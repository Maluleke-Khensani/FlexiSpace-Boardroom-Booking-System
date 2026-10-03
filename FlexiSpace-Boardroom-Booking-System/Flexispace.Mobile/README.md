# Flexispace Mobile (.NET MAUI)

Boardroom booking UI for Centurion, Houghton Estate, and Eagle Canyon — branded to match [flexispace.net.za](https://flexispace.net.za/).

## Run

```powershell
cd "Flexispace App\Flexispace.Mobile"
dotnet workload install maui   # once
dotnet restore
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

Android: `dotnet build -f net10.0-android` (emulator/device required).

## Demo login

- Email: `staff@flexispace.net.za`
- Password: `demo123`

More accounts on the login screen / in `API_CONTRACT.md`.

## Structure

- `Views/` — XAML screens
- `ViewModels/` — MVVM (CommunityToolkit.Mvvm)
- `Services/` — interfaces + `Mock/` implementations
- `Models/` — shared domain types
- `API_CONTRACT.md` — what the backend team should build

Backend is not required yet: mocks power the full navigation and booking flow.
