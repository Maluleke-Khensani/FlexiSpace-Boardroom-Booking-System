# Flexispace Mobile (.NET MAUI)

Boardroom booking app for Centurion, Houghton Estate, and Eagle Canyon — branded to match [flexispace.net.za](https://flexispace.net.za/).

This project is a **Windows-first MAUI prototype**. It runs entirely on **mock services** today, so you can explore the full booking flow without a backend.

---

## Prerequisites

Install these before you open the project:

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11** (build 19041+) | Required for the current target framework |
| **[.NET SDK 10](https://dotnet.microsoft.com/download)** | Check with `dotnet --version` (10.0.x or newer) |
| **.NET MAUI workload** | Installed once via the command below |
| **Visual Studio 2022 17.8+** *(optional)* | Install the **.NET Multi-platform App UI development** workload, or use the CLI only |

---

## First-time setup

### 1. Clone the repository

```powershell
git clone https://github.com/EMGPRS/insy7315-2026-task-1-ribaorearabetse.git
cd insy7315-2026-task-1-ribaorearabetse
```

The mobile app lives in `Flexispace App\Flexispace.Mobile`.

### 2. Install the MAUI workload

Run this once on your machine:

```powershell
dotnet workload install maui
```

Verify it installed:

```powershell
dotnet workload list
```

You should see `maui` and `maui-windows` in the list.

### 3. Restore dependencies

```powershell
cd "Flexispace App\Flexispace.Mobile"
dotnet restore
```

### 4. Build the app

```powershell
dotnet build -f net10.0-windows10.0.19041.0
```

### 5. Run the app

```powershell
dotnet run -f net10.0-windows10.0.19041.0
```

On Windows, the app opens in a fixed **400×860** window sized like a phone for demos.

---

## Running from Visual Studio

1. Open `Flexispace App\Flexispace.Mobile.sln` (or the `.slnx` file).
2. Set **Flexispace.Mobile** as the startup project.
3. Choose the **Windows Machine** target.
4. Press **F5** to run.

If the Windows target does not appear, install the **.NET MAUI** workload in Visual Studio Installer and restart the IDE.

---

## Demo login

All mock accounts use password: **`demo123`**

You can type credentials manually or tap a **demo role tile** on the login screen.

| Email | Role | What you can test |
|-------|------|-------------------|
| `staff@flexispace.net.za` | Staff | Book rooms, view own bookings, alerts |
| `rebecca@flexispace.net.za` | Centre Manager (Houghton) | View bookings, block rooms, manage centre |
| `antoinette@flexispace.net.za` | Centre Manager (Eagle Canyon) | Same as above, Eagle Canyon scope |
| `lesego@flexispace.net.za` | Centre Manager (Eagle Canyon) | Same as above, Eagle Canyon scope |
| `admin@flexispace.net.za` | Administrator | Full manage console, users, rooms, reports |
| `client@example.com` | Client | Book rooms as an external client |

Each role sees a **different tab bar** — not the same screen with hidden buttons.

---

## Project structure

```
Flexispace.Mobile/
├── Views/              XAML screens
├── ViewModels/         MVVM (CommunityToolkit.Mvvm)
├── Services/           Interfaces + Mock/ implementations
├── Models/             Domain types (bookings, rooms, users, etc.)
├── Helpers/            Role permissions, shell/tab helpers
├── Platforms/Windows/  Windows-specific code (incl. chatbot prototype)
├── Resources/          Images, fonts, styles
├── API_CONTRACT.md     Backend API the app expects
└── MauiProgram.cs      DI registration (swap mocks for HTTP later)
```

---

## Backend

No API is required for local development. Mock data is seeded in `Services/Mock/MockDataStore.cs` and `Services/SeedData.cs`.

When the backend is ready, replace the mock service registrations in `MauiProgram.cs` with HTTP implementations. See **`API_CONTRACT.md`** for endpoints and payloads.

---

## Troubleshooting

**`dotnet workload install maui` fails**  
Run the terminal as Administrator, or install MAUI through Visual Studio Installer.

**Build error: workload not found**  
Run `dotnet workload restore` inside `Flexispace.Mobile`, then rebuild.

**App window is blank or crashes on startup**  
Clean and rebuild:

```powershell
dotnet clean
dotnet build -f net10.0-windows10.0.19041.0
```

**Android build (optional)**  
The project is currently configured for Windows only. To target Android later, add `net10.0-android` to `TargetFrameworks` in `Flexispace.Mobile.csproj`, install the Android SDK/emulator, then:

```powershell
dotnet build -f net10.0-android
```

---

## INSY7315 submission notes

- Repository: [insy7315-2026-task-1-ribaorearabetse](https://github.com/EMGPRS/insy7315-2026-task-1-ribaorearabetse)
- Team attendance register: `INSY7315_Team Attendence.pdf` (repo root)
