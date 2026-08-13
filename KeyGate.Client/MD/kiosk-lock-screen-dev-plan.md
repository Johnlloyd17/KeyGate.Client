# Kiosk Access & Lock Screen Management System — Development Plan

## Project Name: **KeyGate**

The solution and its components will be named under the `KeyGate` umbrella:

| Component | Project Name | Output |
|---|---|---|
| Backend API | `KeyGate.Api` | Web API (hosted, not an exe) |
| MAUI Desktop Lock Screen Client | `KeyGate.Client` | **`KeyGate.Client.exe`** (the app that runs on each shared computer) |
| Admin Portal | `KeyGate.Admin` | Web app (hosted, not an exe) |
| Shared Models/DTOs | `KeyGate.Shared` | Class library referenced by Api + Client |

`KeyGate.Client.exe` is the file that gets installed/auto-started on every
kiosk computer — it's the actual lock screen application referenced throughout
this plan.

## 1. Project Overview

A centrally-managed lock screen system for shared/kiosk computers. Each desktop
runs a **.NET MAUI lock screen client**. Individuals are pre-registered by an
**administrator**, then complete self-registration by **scanning a QR code**,
which gives them a personal **access key**. That key unlocks **any** available
(not-yet-unlocked) computer running the client. Every unlock/lock event is
logged as a session.

This is NOT an offline app — it requires a central backend so that:
- Admin-registered users and generated keys are recognized across *all* desktops
- Session logs from every desktop are visible in one place
- Lock screen branding (background, logo, title) can be pushed/updated centrally

**Core idea:** Admin registers people → system generates a QR/token per person →
person scans QR → completes registration → receives a key → uses that key on
any locked desktop client → desktop unlocks and logs the session.

---

## 2. Core User Flow Summary

```
[Admin Portal]
   1. Admin logs in
   2. Admin pre-registers an individual (name, email/ID, department, etc.)
   3. System generates a unique Registration Token + QR code for that individual
   4. Admin shares/prints/displays the QR code

[Individual / End User]
   5. Individual scans QR code with their phone
   6. Opens a mobile-friendly Registration Page (pre-filled with their token)
   7. Confirms/completes their details, sets or receives an Access Key
   8. Registration is marked "Completed" in the system

[Desktop / Computer Client — .NET MAUI]
   9. Computer shows the Lock Screen (custom background, logo, title)
  10. User enters their Access Key
  11. Client validates the key against the backend API
  12. If valid AND the key isn't already active on another unlocked machine:
        - Lock screen hides
        - A Session record starts (user, device, start time)
  13. On logout / idle timeout / manual re-lock:
        - Session record closes (end time, duration)
        - Lock screen reappears
```

---

## 3. System Architecture

```
                        ┌───────────────────────────┐
                        │   Backend API (ASP.NET     │
                        │   Core Web API)             │
                        │  - Auth (admin + keys)      │
                        │  - Users / Devices / Tokens │
                        │  - Sessions log              │
                        │  - Lock screen config        │
                        └─────────────┬──────────────┘
                                      │  REST / SignalR
        ┌────────────────────┬───────┴─────────┬─────────────────────┐
        │                    │                  │                    │
┌───────▼────────┐  ┌────────▼────────┐  ┌──────▼───────┐   ┌────────▼────────┐
│ Admin Portal    │  │ Registration     │  │ MAUI Desktop  │   │ MAUI Desktop    │
│ (Web, Blazor or │  │ Page (public,    │  │ Client #1     │   │ Client #2 ...N  │
│ React/Next.js)  │  │ mobile-friendly, │  │ (lock screen) │   │ (lock screen)   │
│                 │  │ opened via QR)   │  │               │   │                 │
└─────────────────┘  └──────────────────┘  └───────────────┘   └─────────────────┘
```

- **Backend API** — single source of truth. All apps are just clients of it.
- **Admin Portal** — web-based so admins can manage from anywhere on the network.
- **Registration Page** — lightweight public web page, opened from the QR link.
- **MAUI Desktop Client** — the actual lock screen running on each shared PC.
- **SignalR (optional but recommended)** — lets the Admin Portal see device
  status (locked/unlocked, who's on it) in real time.

---

## 4. Tech Stack

| Layer | Recommendation | Why |
|---|---|---|
| Desktop lock screen | **.NET MAUI** (Windows target primarily via WinUI head) | You already know MAUI + MVVM |
| Backend API | **ASP.NET Core Web API (.NET 8/9)** | Same ecosystem as MAUI, easy to share DTOs |
| Database | **PostgreSQL** or **SQL Server** (central, not SQLite — multiple devices write to it) | Needs concurrent multi-device access |
| ORM | **Entity Framework Core** | Standard, works well with ASP.NET Core |
| Admin Portal | **Blazor Server/WebAssembly** (stays 100% C#) *or* **Next.js/React** (your existing web skill set) | Either works; Blazor keeps one language across the stack |
| Registration Page | Simple **Razor Page** or lightweight React page | Just a form, needs to be fast and mobile-friendly |
| QR generation | **QRCoder** (.NET NuGet package) | Generates QR codes server-side |
| Real-time device status | **SignalR** | Push lock/unlock state to Admin Portal instantly |
| Auth (Admin) | **ASP.NET Core Identity + JWT** | Standard, secure |
| Auth (End user key) | Custom hashed key lookup (not full Identity) | Keys are simple shared secrets, not full accounts |
| Hosting | Local always-on host machine on your LAN (see section 6.6) | No internet exposure needed — reachable over WiFi/Ethernet by every kiosk on the same network |

---

## 5. Database Schema (initial draft)

**Admins**
- Id, FullName, Email, PasswordHash, Role, CreatedAt

**Individuals** (pre-registered by admin)
- Id, FullName, Email/EmployeeId, Department, Status (`Pending` / `Registered`),
  CreatedByAdminId, CreatedAt

**RegistrationTokens**
- Id, IndividualId, Token (GUID), QrCodeUrl, ExpiresAt, IsUsed, CreatedAt

**AccessKeys**
- Id, IndividualId, KeyHash, IsActive, CreatedAt, LastUsedAt

**Devices** (each computer running the MAUI client)
- Id, DeviceName, DeviceFingerprint (hardware id), Location, Status (`Locked` /
  `Unlocked`), LastSeenAt

**LockScreenConfig**
- Id, DeviceId (nullable = global default), BackgroundImageUrl, LogoUrl, Title,
  UpdatedAt

**Sessions**
- Id, IndividualId, DeviceId, StartedAt, EndedAt, DurationSeconds, EndReason
  (`Logout` / `IdleTimeout` / `ForcedByAdmin`)

---

## 6. Database & Network Communication

### 6.1 Core principle: desktops never touch the database directly

The database is **centralized** on a server. No desktop, no admin browser, and
no phone (during registration) ever connects to it directly — everything goes
through the **Backend API** over HTTPS. The database only ever has one client:
the API itself.

```
Desktop A ──┐
Desktop B ──┼──HTTPS──▶  Backend API (KeyGate.Api)  ──▶  PostgreSQL Database
Desktop C ──┘                                              (single source of truth)
Admin Portal ───HTTPS───▶      ▲
Phone (registration) ──HTTPS──▶
```

This means every desktop only needs network access to the **API server over
the local WiFi/LAN** (see section 6.6) — never direct database credentials,
which keeps the DB far easier to secure and keep consistent.

### 6.2 Registration process over the network

1. Admin's browser → HTTPS/HTTP → `POST /api/individuals` → API writes to the
   DB (`Individuals`, `RegistrationTokens` tables) → API generates a QR code
   that encodes the host machine's local URL, e.g.
   `http://192.168.1.50:5000/register/{token}` (see section 6.6).
2. Phone scans the QR → opens that URL → hits the **Registration Page**, which
   calls `GET /api/registration/{token}` to pull the pre-filled info and
   confirm the token hasn't expired or already been used.
3. User submits → `POST /api/registration/{token}/complete` → API writes a new
   hashed `AccessKey` row, marks the token used, and returns the plain key
   **once** so the user can see/save it.
4. Because the API is hosted locally (not on the public internet), the phone
   **must be connected to the same WiFi/LAN** as the host machine to complete
   registration — it won't be reachable from outside that network.

### 6.3 Unlocking process over the network

1. MAUI client on a desktop → `POST /api/sessions/unlock` with
   `{ key, deviceId }`.
2. API hashes the submitted key, looks it up, and checks: is it valid? Is the
   individual active? Is *this specific device* currently locked (not already
   occupied)?
3. If all checks pass, the API performs this **as a single database
   transaction** — creating the `Session` row and flipping `Device.Status` to
   `Unlocked` together — so two desktops can't both succeed with the same key
   at the same instant (a real race condition once many physical machines are
   hitting the API concurrently).
4. API returns success → client hides the lock screen.
5. On logout/idle timeout → `POST /api/sessions/{id}/end` → DB closes the
   session and flips the device back to `Locked`.
6. If a desktop loses connectivity to the API, it **cannot unlock** — this is
   intentional (see Security Considerations): there is no offline key
   validation, so the client shows a connection error until network returns.

### 6.4 Database choice

| Option | Verdict |
|---|---|
| **PostgreSQL** | **Recommended for the central database.** Free, handles concurrent writes from many devices well (MVCC), strong EF Core support via Npgsql, easy to self-host or move to a managed service later. |
| SQL Server (Express) | Fine alternative, very native to .NET, but the free Express edition has size/scaling limits if this grows. |
| MySQL | Workable given your PHP/MySQL background (via Pomelo.EntityFrameworkCore.MySql), but PostgreSQL generally handles concurrent write-heavy workloads a bit more gracefully. |
| SQLite | **Not for the central database** — it's file-based and effectively single-writer, a bad fit once multiple desktops hit it through concurrent API requests. It remains the right choice for the **local on-device cache** on each MAUI client (just for displaying last-known lock screen branding while offline), not as the source of truth. |

### 6.5 PostgreSQL setup steps

**A. Install PostgreSQL (on the server that will host the database)**
1. Download and install PostgreSQL for your OS (postgresql.org, or via a
   package manager — `apt install postgresql` on Ubuntu, or use a Docker
   container for easy setup: `docker run --name keygate-db -e POSTGRES_PASSWORD=yourpassword -p 5432:5432 -d postgres:16`)
2. Confirm it's running: `psql -U postgres` (or `docker exec -it keygate-db psql -U postgres`)
3. Create the database and a dedicated app user (don't use the `postgres`
   superuser from the API):
   ```sql
   CREATE DATABASE keygate_db;
   CREATE USER keygate_app WITH ENCRYPTED PASSWORD 'strong-password-here';
   GRANT ALL PRIVILEGES ON DATABASE keygate_db TO keygate_app;
   ```

**B. Connect it to the ASP.NET Core API (`KeyGate.Api`)**
4. Add the EF Core Postgres provider:
   ```
   dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
   ```
5. Add the connection string to `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "KeyGateDb": "Host=localhost;Port=5432;Database=keygate_db;Username=keygate_app;Password=strong-password-here"
   }
   ```
6. Register the DbContext in `Program.cs`:
   ```csharp
   builder.Services.AddDbContext<KeyGateDbContext>(options =>
       options.UseNpgsql(builder.Configuration.GetConnectionString("KeyGateDb")));
   ```

**C. Create the schema via EF Core Migrations**
7. Define your entities (`Individual`, `RegistrationToken`, `AccessKey`,
   `Device`, `LockScreenConfig`, `Session`, `Admin`) matching section 5.
8. Generate the first migration:
   ```
   dotnet ef migrations add InitialCreate
   ```
9. Apply it to the database:
   ```
   dotnet ef database update
   ```
10. Verify the tables exist: `psql -U keygate_app -d keygate_db -c "\dt"`

**D. Production hardening (before real rollout)**
11. Turn off remote superuser access; restrict PostgreSQL to only accept
    connections from the API server's IP (`pg_hba.conf`).
12. Enable SSL for the database connection (`Ssl Mode=Require` in the
    connection string) if the API and DB aren't on the same private network.
13. Set up automated backups (`pg_dump` on a schedule, or your hosting
    provider's managed backup feature).
14. Store the real connection string/password in a secrets manager or
    environment variable — never commit it to source control.

### 6.6 LAN-Only Hosting (No Internet Required)

"Centralized" only means *one* database that every desktop shares — it does
**not** require internet hosting. For this project, PostgreSQL and
`KeyGate.Api` can both live on a single machine inside your own WiFi/LAN, and
every other component just needs to be connected to that same network.

```
        [Router / WiFi Access Point]
                    │
     ┌──────────────┼──────────────────┬─────────────┐
     │              │                  │             │
[Host Machine]  [Desktop A]       [Desktop B]   [Admin PC / Phone]
 PostgreSQL +      KeyGate.Client     KeyGate.Client
 KeyGate.Api       (lock screen)      (lock screen)
 e.g. 192.168.1.50
```

**Choosing the host machine**
- Any always-on machine works — a spare desktop, a low-power mini PC, or an
  existing office PC that stays on anyway.
- **Wired Ethernet is strongly preferred for this machine specifically** —
  it's the single point every kiosk depends on, so it should be the most
  stable device on the network. The kiosk desktops themselves can stay on
  WiFi without issue.

**Setup steps specific to LAN-only deployment**
1. Install PostgreSQL and `KeyGate.Api` on the host machine (see section 6.5
   for PostgreSQL steps).
2. In `postgresql.conf`, keep `listen_addresses = 'localhost'` — since the API
   runs on the *same* machine as the database, Postgres itself never needs to
   accept connections from other computers. Only the API needs to be reachable
   by the rest of the LAN.
3. Configure the host machine's firewall to allow inbound traffic on the API's
   port (e.g. `5000`) from the local subnet only.
4. Set a **DHCP reservation** on the router so the host machine's local IP
   never changes (e.g. always `192.168.1.50`) — otherwise every kiosk's
   configured API URL breaks after a reboot/reconnect.
5. Point every `KeyGate.Client.exe` and the Admin Portal at the host's local
   IP instead of a public domain, e.g. `http://192.168.1.50:5000`.
6. QR codes generated during registration will also encode this local IP
   (e.g. `http://192.168.1.50:5000/register/{token}`) — meaning the phone
   scanning it must be connected to the **same WiFi network** to complete
   registration. This is expected and even useful: only people physically on
   the network can self-register.
7. HTTPS is optional on a closed LAN (no public exposure), but access keys
   must still be hashed in the database regardless — encrypting the wire
   protects against anything sniffing local traffic if that matters for your
   environment; a self-signed certificate is enough if you want it.
8. Consider a UPS (battery backup) for the host machine — since every kiosk
   depends on it being reachable, an unexpected shutdown there locks everyone
   out until it's back online.

### 6.7 Moving the Database Between Machines (pgAdmin4 Export/Import)

This is a manual **ops/deployment step**, not an app feature — it's how you
carry your PostgreSQL database from your dev machine over to the host machine
(or any other device) using pgAdmin4's built-in Backup/Restore tools instead
of the command line.

**When you'd do this**
- Moving your dev database (schema + test data) to the LAN host machine during
  Phase 5 deployment (section 12).
- Cloning the DB to a second dev machine or a teammate's computer.
- Taking a manual snapshot/backup before a risky change.

**A. Export (Backup) from the source machine**

1. Open pgAdmin4 → expand **Servers → your Postgres server → Databases →
   `keygate_db`**.
2. Right-click `keygate_db` → **Backup...**
3. Choose a filename/location (e.g. `keygate_db_backup.backup`), keep format
   as **Custom** (recommended — compressed, restorable selectively) or
   **Plain** (a readable `.sql` file, useful if you want to inspect/edit it).
4. Under the "Dump Options" tabs, the defaults (schema + data) are fine for a
   full copy. Click **Backup** and wait for it to finish.

**B. Transfer the file**

5. Copy the resulting `.backup` (or `.sql`) file to the target machine — USB
   drive, shared network folder, or however you move files between the two.

**C. Import (Restore) on the target machine**

6. On the target machine, make sure PostgreSQL is installed and a matching
   empty database exists first (see section 6.5 step A — `CREATE DATABASE
   keygate_db;`).
7. In pgAdmin4 on the target machine, expand **Servers → your Postgres server
   → Databases**, right-click `keygate_db` → **Restore...**
8. Select the `.backup` file, keep format matching what you exported with
   (**Custom** or **Plain**), and click **Restore**.
9. Verify the tables and data came through: expand `keygate_db` → **Schemas →
   public → Tables**, or right-click → **Query Tool** and run a quick
   `SELECT * FROM "Individuals";`.

**Notes**
- If the target database already has EF Core migrations applied (empty
  tables, no data), a plain **data-only** backup/restore may be more
  appropriate than a full schema+data one — otherwise you can get schema
  conflicts. pgAdmin4's Backup dialog lets you restrict to data-only under
  "Dump Options → Only data."
- For real production data, prefer exporting the **schema via EF Core
  Migrations** (section 6.5) and only moving genuinely necessary data (e.g.
  admin accounts) this way — not leftover dev/test rows.
- This is equivalent to running `pg_dump`/`pg_restore` from the terminal —
  pgAdmin4 is just a GUI wrapper around the same PostgreSQL tools, so either
  approach produces the same result.

---

## 7. Application Components

### 7.1 Admin Portal
- Admin login (secure, ASP.NET Core Identity + JWT or cookie auth)
- **Individuals management**: add/edit individuals, view registration status,
  regenerate QR/token, deactivate a person
- **QR code display/print** per individual
- **Devices management**: view all computers, their current status
  (locked/unlocked/who's on it), rename devices
- **Lock screen customization**: upload background image, upload logo, set
  title text — globally or per device
- **Session logs**: searchable/filterable table (by user, device, date range),
  exportable to CSV/Excel
- **Live dashboard** (optional, via SignalR): real-time grid of all devices
  and their lock state

### 7.2 Public Registration Page (opened via QR)
- Reads the token from the URL (e.g. `https://yourapp.com/register/{token}`)
- Validates token (not expired, not already used)
- Shows the individual's pre-filled info (from admin's pre-registration)
- Lets them confirm details and **generates their Access Key**
  (system-generated, shown once, or user sets their own PIN — your choice)
- Marks token as used, individual status → `Registered`

### 7.3 MAUI Desktop Lock Screen Client
- **Kiosk-style full-screen window** that:
  - Displays background image + logo + title (pulled from API, cached locally)
  - Has a key-entry field (PIN pad style or textbox)
  - On submit, calls API to validate the key
  - On success: hides/minimizes itself, notifies API session started
  - Monitors for logout trigger (manual "Lock" button, idle timer, or Windows
    lock event) → notifies API session ended → reshows lock screen
- Runs as a **Windows startup app** (or Windows service + app pairing) so it's
  always active on that machine
- Caches the last-known lock screen config locally (SQLite) so it still shows
  *something* correct if network briefly drops, but key validation always
  requires live API access (security requirement — can't unlock offline)

### 7.4 Backend API (`KeyGate.Api`)

The API is the single source of truth for the entire system — every other
component (Admin Portal, Registration Page, and every `KeyGate.Client.exe`)
is just a client of it. Nothing talks to the database except this API.

**Responsibilities**
- Owns all data access to PostgreSQL via EF Core — no other component connects
  to the database directly (see section 6.1)
- Enforces every business rule centrally, so the rules are the same no matter
  which desktop or portal is calling in:
  - A registration token can only be used once and must not be expired
  - An access key must be valid, active, and belong to a `Registered`
    individual
  - A device can only be unlocked if it isn't already `Unlocked`
  - Unlock + session-start happen as **one atomic transaction** to prevent the
    same key unlocking two computers at the same instant
- Generates QR codes server-side (QRCoder) when an admin registers an
  individual, and generates/hashes access keys during registration completion
- Issues and validates:
  - **Admin auth** — JWT (or cookie) issued at `/api/auth/admin/login`,
    required on all `/api/individuals`, `/api/devices`, `/api/lockscreen-config`,
    and `/api/sessions` (read) endpoints
  - **Device auth** — each `KeyGate.Client.exe` authenticates itself with a
    device credential (API key or client cert) issued at first-run
    registration, so only known kiosk machines can call the unlock/session
    endpoints
- Tracks live device state (`Locked` / `Unlocked`) and session history,
  exposed to the Admin Portal for the logs table and the live dashboard
- Serves the current lock screen branding (background image URL, logo URL,
  title) per device or globally, so a `KeyGate.Client` can refresh its display
  without redeploying anything
- Rate-limits the unlock endpoint to blunt key brute-forcing (see Security
  Considerations)
- Optionally broadcasts real-time events over a **SignalR hub**
  (`/hubs/devices`) so the Admin Portal's live dashboard updates instantly
  when any device locks/unlocks, without polling

**Internal structure (suggested)**
```
KeyGate.Api/
├── Controllers/
│   ├── AuthController.cs
│   ├── IndividualsController.cs
│   ├── RegistrationController.cs
│   ├── DevicesController.cs
│   ├── LockScreenConfigController.cs
│   └── SessionsController.cs
├── Data/
│   ├── KeyGateDbContext.cs
│   └── Migrations/
├── Entities/            (Admin, Individual, RegistrationToken, AccessKey,
│                          Device, LockScreenConfig, Session)
├── Services/
│   ├── QrCodeService.cs
│   ├── KeyHashingService.cs
│   ├── SessionService.cs   (owns the atomic unlock+session-start logic)
│   └── DeviceAuthService.cs
├── Hubs/
│   └── DeviceStatusHub.cs   (SignalR)
└── Program.cs / appsettings.json
```

---

## 8. Key User Flows (detailed)

**A. Admin pre-registers a person**
1. Admin fills "Add Individual" form → POST `/api/individuals`
2. API creates Individual (`Pending`) + RegistrationToken + QR code
3. Admin portal shows/downloads the QR

**B. Individual self-registers**
1. Scans QR → opens `/register/{token}`
2. Page calls `GET /api/registration/{token}` to validate + fetch prefilled data
3. Individual confirms → `POST /api/registration/{token}/complete`
4. API generates Access Key, hashes + stores it, returns the plain key **once**
   to display to the user (they must save it — like a one-time password reveal)

**C. Using the key on a desktop**
1. MAUI client: user types key → `POST /api/sessions/unlock` with
   `{ key, deviceId }`
2. API checks: key valid? individual active? device currently locked (not
   already occupied)?
3. If all good → creates Session, sets Device.Status = `Unlocked`, returns
   success → client hides lock screen
4. On lock/logout → `POST /api/sessions/{id}/end` → Device.Status = `Locked`

**D. Admin customizes a lock screen**
1. Admin uploads image/logo, types title → `POST /api/lockscreen-config`
   (global or per-device)
2. Desktop clients poll (or receive via SignalR) the updated config and
   refresh their display

---

## 9. API Endpoint Reference (draft)

```
Auth
  POST   /api/auth/admin/login

Individuals (Admin only)
  GET    /api/individuals
  POST   /api/individuals
  PUT    /api/individuals/{id}
  DELETE /api/individuals/{id}
  POST   /api/individuals/{id}/regenerate-token

Registration (public, token-gated)
  GET    /api/registration/{token}
  POST   /api/registration/{token}/complete

Devices
  GET    /api/devices
  POST   /api/devices/register        (client self-registers on first run)
  PUT    /api/devices/{id}

Lock Screen Config
  GET    /api/lockscreen-config?deviceId=
  POST   /api/lockscreen-config

Sessions
  POST   /api/sessions/unlock
  POST   /api/sessions/{id}/end
  GET    /api/sessions                (admin, filterable)

Real-time (SignalR hub)
  /hubs/devices   -> broadcasts DeviceStatusChanged events
```

---

## 10. MAUI Project Structure (MVVM)

```
KeyGate.Client/
├── Models/
│   ├── LockScreenConfig.cs
│   ├── UnlockRequest.cs
│   └── SessionInfo.cs
├── ViewModels/
│   ├── LockScreenViewModel.cs
│   └── UnlockedShellViewModel.cs (optional, if client shows anything post-unlock)
├── Views/
│   ├── LockScreenPage.xaml
│   └── UnlockedShellPage.xaml
├── Services/
│   ├── ApiService.cs          (HttpClient wrapper for backend calls)
│   ├── DeviceIdentityService.cs (reads/generates a stable device fingerprint)
│   ├── LocalCacheService.cs   (SQLite cache for last-known config)
│   └── SessionMonitorService.cs (idle timer, lock/logout detection)
├── Platforms/
│   └── Windows/                (startup registration, kiosk-mode window flags)
├── App.xaml / MauiProgram.cs
└── appsettings.json            (API base URL, polling interval, etc.)
```

Same MVVM + local SQLite caching pattern you used on the teleprompter project —
the difference here is the **source of truth lives on the server**, and the
local SQLite is just a cache/fallback for display, never for key validation.

---

## 11. Security Considerations

- **Never store access keys in plain text** — hash them (e.g. BCrypt/Argon2)
  same as passwords.
- **Registration tokens must expire** (e.g. 24–48 hrs) and be single-use.
- **Rate-limit** the unlock endpoint to prevent key brute-forcing.
- **Device authentication**: each MAUI client should have its own device
  credential (API key or client cert) so random machines can't call the API.
- **One active session per key**: prevent the same key from unlocking two
  computers simultaneously (unless that's intentionally allowed).
- **Idle timeout**: auto re-lock + close session after N minutes of inactivity.
- **Encrypt the wire where practical**: on the LAN this is optional since
  there's no public exposure (see section 6.6), but a self-signed certificate
  for the API is still recommended, especially for the registration page
  which handles personal data.
- **Audit trail**: keep session logs immutable (no hard deletes, only status
  flags) for accountability.

---

## 12. Development Roadmap (Phased)

### Phase 1 — Backend Foundation (Week 1–2) ✅
- ✅ Set up ASP.NET Core Web API + EF Core + database
  (PostgreSQL install/setup still pending — see section 6.5, then `dotnet ef database update`)
- ✅ Implement Individuals, RegistrationTokens, AccessKeys, Devices, Sessions entities + migrations
- ✅ Implement Admin auth (login, JWT)
- ✅ Implement Individuals CRUD + QR generation (QRCoder)

### Phase 2 — Registration Flow (Week 2–3) ✅
- ✅ Build public Registration Page (token validation, complete registration)
  (Razor Page hosted in `KeyGate.Api` at `/register/{token}`, backed by
  `GET/POST /api/registration/{token}`, `POST .../complete`)
- ✅ Access key generation + one-time reveal
  (system-generated 6-digit key, shown once on the page, BCrypt-hashed in DB)
- ⬜ End-to-end test: admin creates individual → scan QR → complete registration
  (deferred for now — pending PostgreSQL setup, section 6.5; will be verified later)

### Phase 3 — MAUI Lock Screen Client (Week 3–5) ✅
- ✅ Build full-screen lock UI (background/logo/title binding)
  (`Views/LockScreenPage.xaml` + `ViewModels/LockScreenViewModel.cs`, bound to
  `LockScreenConfig`; background/logo served as `UriImageSource` from the API)
- ✅ Device self-registration on first run (get a DeviceId)
  (`DeviceIdentityService` creates a stable `KG-…` fingerprint; `ApiService`
  calls `POST /api/devices/register`, stores `DeviceId` + `DeviceApiKey` in
  `Preferences`, and sends them as `X-Device-Id`/`X-Device-Api-Key` headers)
- ✅ Key entry → unlock API call → hide/show lock screen
  (`POST /api/sessions/unlock`; on success the page switches to the unlocked
  "Welcome, {name}" state with a Lock button)
- ✅ Idle timer + manual re-lock
  (`SessionMonitorService` dispatcher timer + activity notifications; re-locks
  with `EndReason=IdleTimeout`, manual lock uses `Logout`)
- ✅ Session start/end API calls
  (unlock returns `sessionId`, `individualName`; `POST /api/sessions/{id}/end`)
- ✅ Local SQLite cache for offline-safe display of last-known branding
  (`LocalCacheService` via `sqlite-net-pcl`; cached config shown first, then
  refreshed from the API on a `ConfigRefreshMinutes` timer)
- ✅ App wiring: `appsettings.json` (API base URL, device name prefix, idle
  timeout, config refresh), `AppSettings` loader, DI in `MauiProgram.cs`,
  `App.xaml.cs` launches `LockScreenPage` directly (old `MainPage`/`AppShell`
  removed)
- ✅ Windows kiosk fullscreen flags
  (`AppWindowPresenterKind.FullScreen` applied on window `Created`)

### Phase 4 — Admin Portal (Week 5–7) ✅
- ✅ Admin login
  (`KeyGate.Admin` Blazor Server portal; `Pages/Login.razor` posts to `POST /signin`,
  which calls `/api/auth/admin/login`, sets a cookie auth ticket, then `POST /signout` clears it)
- ✅ Individuals management + QR code display/print
  (`Pages/Individuals.razor`; add/edit/delete, view registration status,
  regenerate token, display/print the QR per individual)
- ✅ Devices list + live status
  (`Pages/Devices.razor`; shows locked/unlocked/who's on it, rename device + location)
- ✅ Lock screen customization UI (upload image/logo, edit title)
  (`Pages/LockScreenConfig.razor`; global or per-device; `POST /api/lockscreen-config/upload`
  stores files in the API's `wwwroot/uploads`, served via `UseStaticFiles`; preview)
- ✅ Session logs table with filters + CSV export
  (`Pages/SessionLogs.razor`; filter by individual/device/date range, client-side CSV export)
- ✅ SignalR live dashboard
  (`Hubs/DeviceStatusHub.cs` at `/hubs/devices`; `SessionService` broadcasts
  `DeviceStatusChanged` on unlock/session-end; the portal subscribes server-side via
  `DeviceStatusListener` and updates the Dashboard/Devices grids in real time)

### Phase 5 — Hardening & Deployment (Week 7–8)
- Rate limiting, device auth, key hashing review, optional self-signed HTTPS
- Windows kiosk-mode packaging for the MAUI client (auto-start, prevent
  alt-tab/close if desired)
- Set up the local host machine: fixed local IP via DHCP reservation, install
  PostgreSQL + `KeyGate.Api`, configure firewall for LAN-only access (see
  section 6.6)
- Move the dev database over to the host machine using pgAdmin4's
  Backup/Restore, or recreate the schema fresh via EF Core Migrations (see
  section 6.7)
- Pilot on a couple of real machines on the WiFi before full rollout

---

## 13. Future Enhancements (optional, post-MVP)

- Push notifications to admin when a device is left unlocked too long
- Facial recognition or NFC card as an alternative to typing the key
- Per-department lock screen branding
- Analytics dashboard (usage per individual, per device, peak hours)
