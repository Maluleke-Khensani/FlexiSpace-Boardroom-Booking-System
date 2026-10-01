# FlexiSpace Test Client

A small React + Vite page used to test Microsoft (Entra ID) sign-in against the FlexiSpace API and call protected endpoints with the resulting token. It isn't part of the product; the real clients are `Flexispace.Web` and `Flexispace.Mobile`.

## Run it

```powershell
cd FlexiSpace.TestClient
npm install
npm run dev
```

Open `http://localhost:5173`. The API must be running on `https://localhost:7055`; its CORS policy (`AllowReactTestClient` in `FlexiSpace.API/Program.cs`) allows this origin.

Sign-in settings are in `src/authConfig.js`. A browser app's client ID and tenant ID are public by design (anyone can read them from the page), so no secret belongs in this folder.
