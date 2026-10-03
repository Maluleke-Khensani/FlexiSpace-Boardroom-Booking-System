How to connect the Web and Mobile clients to the FlexiSpace API

Overview
This document explains how the web and mobile applications should authenticate users via Microsoft Entra (Azure AD / Entra ID) and call the FlexiSpace API securely.

Prerequisites
- Entra app registrations have been created for API and each client (web SPA, mobile app, server web).
- Redirect URIs, client IDs, secrets, and scopes are configured in Entra and stored in user-secrets for development.
- The API exposes a scope (e.g., api://<api-client-id>/access) that client apps request.

Recommended auth flows per client
- Web SPA: Authorization Code + PKCE (no client secret)
- Mobile (iOS/Android): Authorization Code + PKCE using MSAL mobile SDK (no client secret)
- Server-side web app: Authorization Code with client secret (confidential client)

Client responsibilities
1. Perform sign-in with Entra (authorization code + PKCE for public clients).
2. Exchange authorization code for tokens (access_token, refresh_token).
3. Store access_token & refresh_token securely (mobile: Keychain/Keystore, web SPA: in-memory or secure cookie; avoid localStorage for long-term storage).
4. For each API call, add header: Authorization: Bearer <access_token>
5. After sign-in, call GET /api/user/me to retrieve the local FlexiSpace profile (id, role, locationId, email). Use this to decide UI features.
6. Optionally call GET /api/notification/unread-count to populate notification badge.

API requirements (server-side)
- JWT validation middleware must be configured in Program.cs (AddAuthentication + AddJwtBearer). Use Authority = https://login.microsoftonline.com/{TenantId}/v2.0 and validate Audience = API client id or accepted scopes.
- [Authorize] attributes must protect endpoints. The /api/user/me endpoint allows clients to discover their mapped FlexiSpace user profile.
- Configure CORS to allow the web SPA origin.

Example: caller flow (Web SPA with PKCE)
1. Client redirects user to Entra authorize endpoint with client_id, response_type=code, redirect_uri, scope (openid profile api://<api-id>/access), code_challenge.
2. Entra authenticates user, redirects back to client at redirect_uri with code.
3. Client exchanges code + code_verifier at Entra token endpoint for access_token and refresh_token.
4. Client calls API endpoints with Authorization header.

Token expiration and refresh
- Access tokens should be short-lived. Use refresh tokens to obtain new access tokens without forcing users to re-login.
- Store refresh tokens securely and implement refresh rotation where possible.

Redirect URIs and client secrets
- Redirect URIs used by clients must be registered in Entra for each app registration.
- Confidential clients (server web) require a client secret; public clients (mobile, SPA) should not use secrets and must use PKCE.

Useful client code samples
- MSAL.js (web SPA) and MSAL.NET (server) snippets are recommended. Provide sample code to the front-end team.

Troubleshooting
- If API returns 401: check token audience, issuer, and expiry. Ensure API's Authority matches the tenant.
- If token is valid but mapping returns 404 from /api/user/me: ensure the Entra object id exists in the local Users table and the mapping service reads the correct claim (oid or sub).

Next steps
- Provide Postman collection or simple client example (I can add this for you).
- Consider adding token-introspection/logging for debugging auth issues.


