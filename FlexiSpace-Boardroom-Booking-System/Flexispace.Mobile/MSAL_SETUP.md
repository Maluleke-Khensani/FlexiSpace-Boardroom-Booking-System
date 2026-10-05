# Microsoft Entra sign-in (MAUI)

## Client (do not change)

- **MobileClientId:** `85378c65-ead1-4b71-956a-389142bd3342` (FlexiSpace-MAUI-Mobile)
- **Not** the SPA id `77163347-…` (that causes `AADSTS9002327` on native)
- **TenantId:** `2427e95a-1238-4880-8236-996db36bc62c` (fspace — set in `azuread.local.json`)

## Redirect URI (platform-specific in code)

| Platform | Redirect used by the app |
|----------|---------------------------|
| **Windows** | `http://localhost` (required by MSAL system browser — any port) |
| Android / iOS | `msal85378c65-ead1-4b71-956a-389142bd3342://auth` |

Entra app **FlexiSpace-MAUI-Mobile** → Authentication → **Mobile and desktop applications** must include:

- `http://localhost` (covers Windows; any port)
- `msal85378c65-ead1-4b71-956a-389142bd3342://auth` (phone)

Allow public client flows = **Yes**.

## Scope

`api://77163347-59be-48f4-8675-535af30a3a53/access_as_user` (shared API with Khumo’s web), with fallbacks to the mobile app’s own API scopes.

## Accounts

The app does **not** hardcode who may sign in. Microsoft Entra validates the email/password on `login.microsoftonline.com`. After a successful sign-in, FlexiSpace links or auto-provisions that user in LocalDB (role guessed from the UPN when no admin row exists yet: `manager*` → Centre Manager, `staff*` → Staff, `*admin*` → Administrator).

If Microsoft shows “account or password is incorrect”, that rejection is from **Entra** (wrong password, locked account, or expired password that must be reset in Entra admin) — not from a FlexiSpace allow-list.

## After browser “Authentication complete”

You should return to the MAUI window automatically. If Entra then shows `AADSTS50011`, only `http://localhost` is missing on that Mobile/desktop platform — Khensani adds that one URI (no fixed port needed).
