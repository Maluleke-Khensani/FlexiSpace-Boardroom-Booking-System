# FlexiSpace 1 API — handover for the mobile developer

**Date:** 5 October 2026  
**Audience:** Mobile app colleague  
**API project:** `FlexiSpace 1`  
**Shared database:** `Server=(localdb)\MSSQLLocalDB;Database=FlexiSpaceDB`

This note covers **API changes made after FlexiSpace 1 was added to the shared workspace**, so the Blazor web app and the existing LocalDB could run together. Entra auth, dual SPA/mobile audiences, `GET /api/User/me`, and `POST /api/User/me/link` were **not** changed in this pass.

Do **not** generate another `AddBlockedPeriods` migration against this database. The table already exists from `20260929091423_AddBlockedPeriods`.

---

## 1. Why these changes were needed

The web app and FlexiSpace 1 share one LocalDB (`FlexiSpaceDB`). FlexiSpace 1 expected a different `BlockedPeriods` shape and a different booking-create contract than what was already in that database.

Failures before the fix:

| Symptom | Cause |
|---------|--------|
| `There is already an object named 'BlockedPeriods'` | Second migration `20261004213134_AddBlockedPeriods` tried to create a table that already existed |
| `User 0 was not found` on `POST /api/booking` | Body `UserId` defaulted to `0`; the original web API used the signed-in user instead |
| `Invalid column name 'CreatedByUserId' / 'StartDate' / 'StartTime' / 'EndDate' / 'EndTime'` | EF queried columns from the new entity; the live table uses `Start`, `End`, `CreatedById` |

---

## 2. What changed (and what did not)

| Area | Changed? | What the mobile app should do |
|------|----------|-------------------------------|
| `POST /api/booking` | Yes | `UserId` is optional. If you omit it or send `0`, the API books as the **signed-in user**. You may still send a positive `UserId`. |
| BlockedPeriods **HTTP JSON** | No | Keep sending and reading `StartDate`, `StartTime`, `EndDate`, `EndTime`, `CreatedByUserId` on the DTO. |
| BlockedPeriods **SQL columns** | Yes (mapping only) | Do not query `StartDate` / `CreatedByUserId` in raw SQL. Live columns are `Start`, `End`, `CreatedById`. |
| Enums in JSON | Unchanged (already in FlexiSpace 1) | `JsonStringEnumConverter` is on. `role` and `status` are **names** (`"Administrator"`, `"Confirmed"`), not `0/1/2`. |
| Auth / Entra / JWT | No | Same bearer token, audiences, and scopes as before. |
| Other booking fields | No | `BoardroomId`, `BookingDate`, `StartTime`, `EndTime`, `Company`, `NumberOfAttendees`, `Notes`, `Equipment`, `Catering` unchanged. |

---

## 3. `POST /api/booking` — `UserId`

`BookingCreateDto` still has `UserId`. FlexiSpace 1 originally used **only** `dto.UserId`. The web client did not send it, so the API looked up user `0`.

**Current rule** in `BookingController.CreateBooking`:

1. If the caller is not a linked FlexiSpace user → `401`.
2. If `dto.UserId` is **greater than 0** → use `dto.UserId` (mobile can keep sending it).
3. If `dto.UserId` is missing or `0` → use the signed-in user from `ICurrentUserService`.

**Recommended for mobile:** send the signed-in user’s id, or omit it and let the API fill it. **Do not send `0`.**

---

## 4. BlockedPeriods — database vs JSON

Two migrations tried to create the same table with **different column names**. The database kept the first shape. Booking create then queried the second shape inside `HasBlockedPeriodConflictAsync`.

### 4.1 Live SQL table (do not change)

| Column | Type | Notes |
|--------|------|--------|
| `Id` | int identity | PK |
| `BoardroomId` | int | FK `Boardrooms.Id` |
| `Start` | datetime2 | Full start timestamp |
| `End` | datetime2 | Full end timestamp |
| `Reason` | nvarchar(1000) | |
| `CreatedById` | int | FK `Users.Id` |
| `CreatedAt` | datetime2 | |

Indexes already on the table: `IX_BlockedPeriods_BoardroomId_Start_End`, `IX_BlockedPeriods_CreatedById`.

### 4.2 Entity mapping now

`FlexiSpace.Core.Entities.BlockedPeriod` matches those columns: `Start`, `End`, `CreatedById`.

HTTP DTOs are still split date/time. `BlockedPeriodController` maps:

- Incoming `CreateBlockedPeriodDto` `StartDate` + `StartTime` → `entity.Start` (`DateTime`)
- Incoming `EndDate` + `EndTime` → `entity.End`
- `CreatedById` is set from the signed-in user, not from the body
- Outgoing `BlockedPeriodResponseDto` still returns `StartDate`, `StartTime`, `EndDate`, `EndTime`, `CreatedByUserId`

If the mobile app talks **only** to the REST API, you should not need blocked-period client changes. If you have raw SQL or a local copy of `BlockedPeriods` with `StartDate` columns, align that copy to `Start` / `End` / `CreatedById`.

### 4.3 Migration `20261004213134_AddBlockedPeriods`

This file is a **second** `AddBlockedPeriods` (id `20261004213134`). History already contains `20260929091423_AddBlockedPeriods`.

`Up()` now creates the table **only if** `dbo.BlockedPeriods` does not exist, so startup `MigrateAsync` no longer throws. On this shared database it is a **no-op**.

Do **not** drop the table to “make the new migration run”.

---

## 5. Enums in JSON

`Program.cs` still uses `JsonStringEnumConverter`. Example: `"role": "Staff"`, `"status": "Confirmed"`.

`UserRole` names: `CentreManager`, `Staff`, `Administrator`, `Client`  
`BookingStatus` names: `Pending`, `Cancelled`, `Completed`, `Confirmed`

The web client was updated to accept **both** names and numbers. If mobile already deserializes string enums, leave it. If it still expects integers only, parse both.

---

## 6. Shared database rules

- One database: `Server=(localdb)\MSSQLLocalDB;Database=FlexiSpaceDB`
- Do not point a second API at a different database unless the whole team switches
- Do not add a third `BlockedPeriods` migration
- Do not `Delete-Database` or drop `BlockedPeriods` to fix migration conflicts
- `__EFMigrationsHistory` may contain extra ids this project does not have as files (for example `AddBoardroomComponents`, `AddBookingReminderTracking`). That is OK. EF only applies **pending** ids from this project
- `PasswordHash` on `Users` is added with an `IF COL_LENGTH` guard in `AddUserPasswordHash`

---

## 7. Files touched in FlexiSpace 1

| File | Change |
|------|--------|
| `FlexiSpace.API/Controllers/BookingController.cs` | Create booking uses `dto.UserId` if `> 0`, otherwise the signed-in user |
| `FlexiSpace.API/Controllers/BlockedPeriodController.cs` | Maps DTO date/time fields to entity `Start` / `End` and `CreatedById` |
| `FlexiSpace.Core/Entities/BlockedPeriod.cs` | Properties `Start`, `End`, `CreatedById` to match SQL |
| `FlexiSpace.Infrastructure/Persistence/Configurations/BlockedPeriodConfiguration.cs` | FK and index on `CreatedById` / `Start` / `End`. Reason max 1000 |
| `FlexiSpace.Infrastructure/services/BookingService.cs` | `HasBlockedPeriodConflictAsync` compares booking window to `block.Start` / `block.End` |
| `FlexiSpace.Infrastructure/Migrations/20261004213134_AddBlockedPeriods.cs` | Create table only if it does not exist |

The Blazor web client (`Flexispace.Web`) was also updated to send `UserId` and to read string enums. That is not required for mobile unless you share that client.

---

## 8. Quick checks after you pull

- [ ] API starts without a `BlockedPeriods` create-table error
- [ ] `POST /api/booking` with a valid Microsoft bearer token succeeds without `User 0 was not found` and without `Invalid column name StartDate`
- [ ] `GET /api/blockedperiod` still returns `StartDate` / `StartTime` / `EndDate` / `EndTime` / `CreatedByUserId`
- [ ] `POST /api/blockedperiod` still accepts `StartDate` / `StartTime` / `EndDate` / `EndTime`
- [ ] `GET /api/User/me` still returns `role` as a string name

---

## 9. What not to do

- Do not rename the HTTP DTO fields to `Start` / `End` unless we agree to version the API together
- Do not scaffold a new `BlockedPeriods` table migration on `FlexiSpaceDB`
- Do not send `UserId = 0` on create booking
- If you need a schema change, add a **new additive** migration (new columns) and tell the web developer before running it on the shared LocalDB

---

## 10. Auth notes still in force (unchanged)

These were already in this folder and still apply:

- Dual JWT audiences: SPA app `77163347-59be-48f4-8675-535af30a3a53` and mobile app `85378c65-ead1-4b71-956a-389142bd3342`
- After login, call `GET /api/User/me`. If `404` and the access token has no email, `POST /api/User/me/link` with `{ "email": "<MSAL username>" }`
- Passwords live in Entra. The API only accepts a Bearer token and maps it to a `Users` row
- Local connection string: `Server=(localdb)\MSSQLLocalDB;Database=FlexiSpaceDB;Trusted_Connection=True;TrustServerCertificate=True`

Keep talking **REST contracts**, not EF property names, unless you are writing SQL.
