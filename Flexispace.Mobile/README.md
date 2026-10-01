# Flexispace Mobile (.NET MAUI)

Boardroom booking app for Centurion, Houghton Estate, and Eagle Canyon — branded to match [flexispace.net.za](https://flexispace.net.za/).

This project is a **Windows-first MAUI app**. It signs in with **Microsoft Entra ID** and talks to the FlexiSpace API (`FlexiSpace/FlexiSpace.API`), so the API must be running for the app to work.

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
git clone https://github.com/Maluleke-Khensani/FlexiSpace-Boardroom-Booking-System.git
cd FlexiSpace-Boardroom-Booking-System
```

The mobile app lives in `Flexispace.Mobile\Flexispace.Mobile`.

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
cd Flexispace.Mobile\Flexispace.Mobile
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

1. Open `Flexispace.Mobile\Flexispace.Mobile.sln`.
2. Set **Flexispace.Mobile** as the startup project.
3. Choose the **Windows Machine** target.
4. Press **F5** to run.

If the Windows target does not appear, install the **.NET MAUI** workload in Visual Studio Installer and restart the IDE.

---

## Signing in

1. Start the API first (`https://localhost:7055`). See the root README for its user secrets.
2. Run the app, tap **Get a quote · Book a room**, then **Sign in with Microsoft**.
3. Sign in with your Microsoft account in the window that opens.

Your role and location come from your FlexiSpace user record (`GET api/user/me`), and each role sees a **different tab bar**. An account that isn't in FlexiSpace's Users table gets a "not set up yet" message; an Administrator has to add it first.

Sign-in and sign-out are recorded in the audit log (`?client=mobile`).

On Windows, sign-in goes through the Windows account picker (WAM), not a browser.

**One-time Azure setup** (on the **Mobile** app registration, `85378c65-…`, not the API's): add a **Mobile and desktop applications** platform with the redirect URI `ms-appx-web://microsoft.aad.brokerplugin/85378c65-ead1-4b71-956a-389142bd3342`, set **Allow public client flows** to Yes, and under **API permissions** add the API's `access_as_user` permission (with admin consent). Without these, sign-in fails with `AADSTS50011`, `AADSTS7000218` or a consent error. Details are in `Services/Real/ApiConfig.cs`.

The token cache is in memory, so you sign in again each time the app starts.

---

## Project structure

```
Flexispace.Mobile/
├── Views/              XAML screens
├── ViewModels/         MVVM (CommunityToolkit.Mvvm)
├── Services/           Interfaces + Real/ (API and MSAL sign-in)
├── Models/             Domain types (bookings, rooms, users, etc.)
├── Helpers/            Role permissions, shell/tab helpers
├── Platforms/Windows/  Windows-specific code (incl. chatbot prototype)
├── Resources/          Images, fonts, styles
├── API_CONTRACT.md     Backend API the app expects
└── MauiProgram.cs      DI registration
```

---

## Backend

The app calls the FlexiSpace API at `ApiConfig.ApiBaseUrl` (`https://localhost:7055` by default). Change it in `Services/Real/ApiConfig.cs` if the API runs elsewhere. The API calls the app makes are listed in **`API_CONTRACT.md`**.

`Services/SeedData.cs` still supplies the room list used for combined rooms and the equipment and catering choices on the booking screen; moving those to the API is a follow-up.

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
