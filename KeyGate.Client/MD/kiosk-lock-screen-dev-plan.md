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
runs a **.NET MAUI lock screen client**. Individuals **self-register** by
opening a shared registration link (QR code or URL), filling in their own
details, and receiving a personal **access key**. That key unlocks **any**
available (not-yet-unlocked) computer running the client. Every unlock/lock
event is logged as a session.

This is NOT an offline app — it requires a central backend so that:
- Self-registered users and generated keys are recognized across *all* desktops
- Session logs from every desktop are visible in one place
- Lock screen branding (background, logo, title) can be pushed/updated centrally

**Core idea:** Admin shares a registration link → individual fills in their own
info → system generates an access key → individual uses that key on any locked
desktop client → desktop unlocks and logs the session.

---

## 2. Core User Flow Summary

```
[Admin Portal]
   1. Admin logs in
   2. Admin clicks "Share Registration Link" → gets QR code + shareable URL
   3. Admin shares/prints/displays the link with individuals

[Individual / End User]
   4. Individual opens the registration link (scans QR or taps URL)
   5. Opens a mobile-friendly Registration Page
   6. Fills in their own details (name, email/ID, department)
   7. Submits → receives a one-time Access Key
   8. Registration is complete — individual appears in the Admin's list

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

**Individuals** (self-registered or admin-created)
- Id, FullName, Email/EmployeeId, Department, Status (`Pending` / `Registered`),
  Sex, Age, Province, CityMunicipality, Barangay, Sectors (JSON array),
  ServiceAvailed, CreatedByAdminId (nullable — null for self-registered), CreatedAt

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

1. Admin shares a registration link (QR code or plain URL) from the Admin
   Portal — the link points to `http://192.168.1.50:5000/register` (see
   section 6.6). The QR code is generated server-side via `POST /api/registration/qr`.
2. Individual opens the link on their phone → hits the **Registration Page**,
   which displays a blank form for them to fill in their details.
3. Individual fills in Full Name, Email/Employee ID, Department (optional) →
   submits → `POST /api/registration/self-register` → API creates the
   `Individual` record, generates a 6-digit `AccessKey`, BCrypt-hashes it, and
   returns the plain key **once** so the user can see/save it.
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
- **Individuals management**: view/edit/delete individuals, view registration
  status; **share registration link** (QR code + Copy Link + Share)
- **Devices management**: view all computers, their current status
  (locked/unlocked/who's on it), rename devices
- **Lock screen customization**: upload background image, upload logo, set
  title text — globally or per device
- **Session logs**: searchable/filterable table (by user, device, date range),
  exportable to CSV/Excel
- **Live dashboard** (optional, via SignalR): real-time grid of all devices
  and their lock state

### 7.2 Public Registration Page (self-service)
- Individual opens the registration link (scans QR or taps shared URL)
- URL: `http://{API_HOST}/register` (no token required)
- Displays a blank form for the individual to fill in:
  Full Name, Email/Employee ID, Department (optional),
  Sex (dropdown), Age (number), Province, City/Municipality, Barangay,
  Sectors (checkboxes: Student, Government Workforce, PWD, LGBTQ,
  Sr. Citizens, OSY, Indigent, Others), Service Availed
- Individual submits → system creates their record + generates Access Key
  (system-generated, shown once)
- Individual uses the key to unlock a desktop

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

**A. Admin shares registration link**
1. Admin clicks "Share Registration Link" → QR modal opens with
   `http://{API_HOST}/register`
2. Admin shares via Print, Copy Link, or Share (WhatsApp/email/SMS)

**B. Individual self-registers**
1. Individual opens the shared link on their phone
2. Registration Page loads at `/register` — blank form
3. Individual fills in Full Name, Email/Employee ID, Department (optional)
4. Individual taps "Complete Registration"
5. `POST /api/registration/self-register` → API creates Individual + generates
   Access Key → returns the plain key **once**
6. Individual saves/screenshot the key

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
  PUT    /api/individuals/{id}
  DELETE /api/individuals/{id}

Registration (public — self-service)
  POST   /api/registration/self-register       → individual fills in own info, gets access key
  POST   /api/registration/qr                  → generates QR code PNG for a given URL

Registration (legacy — token-gated, still available)
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
- ✅ Build self-service Registration Page (`/register` — blank form, no token required)
  (Razor Page hosted in `KeyGate.Api`, individual fills in own Name, Email/ID,
  Department; backed by `POST /api/registration/self-register`)
- ✅ Access key generation + one-time reveal
  (system-generated 6-digit key, shown once on the page, BCrypt-hashed in DB)
- ✅ QR code generation for registration link
  (`POST /api/registration/qr` generates QR PNG for any URL, used by Admin Portal)

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
- ✅ Individuals management + Registration Link sharing
  (`Pages/Individuals.razor`; view/edit/delete individuals, share registration
  link via QR modal with Print/Copy Link/Share options; no manual "Add Individual"
  form — individuals self-register via the shared link)
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
- ✅ Rate limiting on the unlock endpoint
  (`AddRateLimiter` "unlock" policy in `KeyGate.Api/Program.cs` +
  `[EnableRateLimiting("unlock")]` on `POST /api/sessions/unlock`; fixed window,
  5 attempts per minute per device/IP, rejects with HTTP 429 —
  verified: 5 requests pass, the 6th is rejected)
- ✅ Device auth review
  (each `KeyGate.Client.exe` authenticates with its own device credential —
  64-char API key from a CSPRNG, BCrypt-hashed at rest, verified via
  `DeviceAuthService.ValidateAsync` against `X-Device-Id`/`X-Device-Api-Key`;
  anonymous first-run `POST /api/devices/register` stays as the plan requires —
  tradeoff noted in the runbook)
- ✅ Key hashing review
  (access keys + device API keys are BCrypt-hashed via `KeyHashingService`,
  never plain text; the 6-digit access keys rely on the unlock rate limit to
  blunt brute-forcing)
- ✅ Optional HTTPS enabled in dev
  (`UseHttpsRedirection()` + existing `https` launch profile
  `https://localhost:7000`, dev cert trusted via `dotnet dev-certs https
  --trust`; launching with the `https` profile makes HTTP on :5000 redirect to
  HTTPS — verified: `/api/devices` responds over TLS, HTTP returns 307)
- ✅ Windows kiosk-mode packaging: auto-start
  (`Platforms/Windows/StartupRegistration.cs` writes/refreshes
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` →
  `KeyGate.Client.exe` on every launch, hooked from `App.xaml.cs`
  `OnWindowCreated`; combined with the existing `AppWindowPresenterKind.FullScreen` flag)
- ⬜ Prevent alt-tab/close (if desired) — manual Windows kiosk / Assigned
  Access setting, see runbook
- ⬜ Set up the local host machine — manual ops, see runbook
- ⬜ Move the dev database to the host machine — manual ops, see runbook
- ⬜ Pilot on a couple of real machines on the WiFi — manual, see runbook

#### Phase 5 runbook (manual ops — do these on/around the host machine)

1. **Pick the host machine** — any always-on PC (spare desktop, low-power mini
   PC, or an existing office PC). **Wired Ethernet strongly preferred** for it;
   the kiosk desktops can stay on WiFi (section 6.6).
2. **Install PostgreSQL + `KeyGate.Api` on the host** (section 6.5). In
   `postgresql.conf` keep `listen_addresses = 'localhost'` — Postgres never
   needs to accept remote connections because only the API runs on that machine.
3. **Create the DB + app user on the host** (section 6.5, step A), then set the
   real connection string in the host's `KeyGate.Api/appsettings.json` (or an
   environment variable — never commit the real password to source control).
4. **Get the schema (and data) onto the host** — either:
   - pgAdmin4 **Backup** of `keygate_db` on the dev machine, copy the `.backup`
     file over, then **Restore** on the host (section 6.7), or
   - a fresh empty database on the host + `dotnet ef database update --project
     KeyGate.Api` to recreate the schema via EF Core Migrations (section 6.5).
5. **Configure the host firewall** — allow inbound TCP on the API port (e.g.
   `5000`) from the **local subnet only**, nothing public (section 6.6).
6. **Set a DHCP reservation** on the router so the host's local IP never
   changes (e.g. always `192.168.1.50`). Then point every `KeyGate.Client.exe`
   (`ApiBaseUrl`) and the Admin Portal (`KeyGateApi:BaseUrl`) at
   `http://192.168.1.50:5000`. QR codes will encode that same local URL, so
   phones must be on the same WiFi to self-register — expected behavior
   (section 6.6).
7. **HTTPS (optional)** — generate a self-signed certificate on the host, bind
   it to the API, and trust it on each kiosk + admin PC, or keep plain HTTP on
   the closed LAN (section 6.6 + section 11). Dev machines already have the
   trusted dev certificate.
8. **Add a UPS** (battery backup) to the host — every kiosk depends on it being
   reachable (section 6.6).
9. **Kiosk hardening (optional)** — if you want to prevent Alt-Tab / closing the
   app, enable Windows kiosk mode (Assigned Access) for the `KeyGate.Client.exe`
   user on each desktop. The client already runs fullscreen and auto-starts.
10. **Pilot** — deploy `KeyGate.Client.exe` to 1–2 real machines, then verify:
    device self-registration on first run, unlock via a registered key, manual
    lock + idle re-lock, lock screen branding refresh, and the Admin live
    dashboard updating in real time over WiFi.

**Known tradeoff (device auth):** `POST /api/devices/register` is anonymous by
design so a fresh kiosk can self-register on first run. On an *untrusted* LAN an
attacker could re-register a machine and rotate its device key. Acceptable for
the plan's trust model; if the network is ever hostile, gate registration to
admin-approved devices only.

### Phase 6 — Frontend Design Refinement: Modern Minimalist + F/Z Patterns (Week 8–9)

All five previous phases focused on functionality. This phase refines every UI
component to follow a **modern minimalist** design language guided by
**F-pattern** and **Z-pattern** eye-tracking heuristics (see section 15 for the
full rule that applies to all future work as well).

#### 6A. Design System Foundation

Establish shared design tokens before touching individual pages — these tokens
are the single source of truth for visual decisions across all three UI
components.

- **Typography**: single font family (e.g. Inter, Segoe UI, or the system
  default), two weights (regular + semibold), a clear type scale (display,
  heading, body, caption).
- **Color palette**: one neutral scale (gray-50 through gray-900) + one accent
  color for primary CTAs. Minimal accent usage — only on interactive elements
  that need to draw the eye.
- **Spacing scale**: consistent spacing unit (e.g. 4px base → 4, 8, 12, 16, 24,
  32, 48, 64) applied to padding, margins, and gaps.
- **Border radius**: one consistent radius for cards, inputs, buttons (e.g.
  6–8px). No mixed radius styles.
- **Shadows**: one or two elevation levels at most — subtle for cards, slightly
  stronger for dropdowns/modals. No heavy drop shadows.
- **Implementation locations**:
  - Admin Portal: CSS custom properties in `wwwroot/css/` (e.g.
    `:root { --color-accent: #...; --space-md: 16px; }`)
  - Registration Page: same CSS custom properties (shared with Admin Portal if
    co-hosted, or duplicated if standalone)
  - MAUI Client: `Resources/Styles/DesignTokens.xaml` with matching values as
    `StaticResource` keys

**Modern minimalist principles** (apply everywhere):
- Ample whitespace — don't cram elements; let each section breathe.
- Reduce visual clutter — remove unnecessary borders, divider lines, and heavy
  box shadows.
- Limit color usage — background is near-white or near-dark, text is high-contrast,
  accent color appears only on primary actions.
- Consistent component shapes — all cards look like cards, all buttons look like
  buttons, all inputs look like inputs. No page-specific reinvention.

#### 6B. F-Pattern Layout (Admin Portal data-heavy pages)

F-pattern applies to pages where the user **scans rows of data** — the eye
moves across the top (column headers), drops down, scans a shorter second line,
then trails down the left edge looking for keywords (names, statuses, IDs).

**Target pages:**
- `Pages/SessionLogs.razor`
- `Pages/Individuals.razor`
- `Pages/Devices.razor`

**F-pattern rules applied to each page:**

| Rule | Implementation |
|---|---|
| Front-load the most important word in headlines | Table/page heading leads with the key noun: "Active Sessions" not "Sessions That Are Active"; "Registered Individuals" not "List of All Individuals" |
| Left-align navigation and content edges | Sidebar nav items left-aligned; table left edge aligned with page heading; filter panel left-aligned |
| Left-load table columns | First 2–3 columns carry the most critical data (name/device, status, timestamp). Secondary info (duration, actions) goes right |
| No critical info in middle-right | Status badges, action buttons placed at the START of a row or in a dedicated left-aligned action column, not buried mid-row |
| Subheadings at the left edge | Section dividers and group labels flush-left, not centered |
| Scannable bullet/list format for summaries | Dashboard stat cards use left-aligned labels with large numbers, not centered text blocks |

**Specific column ordering for data tables:**

```
SessionLogs:   [Device] [User] [Status] [Started] [Duration] [Actions]
Individuals:   [Seq] [Name] [Sex] [Age] [Province] [City/Municipality] [Barangay] [Sectors] [Service Availed] [Date] [Actions]
Devices:       [Device Name] [Status] [Location] [Last Seen] [Actions]
```

#### 6C. Z-Pattern Layout (visual/action-focused screens)

Z-pattern applies to pages where the user's eye sweeps across a visual layout —
top-left to top-right, diagonal to bottom-left, then bottom-left to bottom-right
where the final CTA sits.

**Target pages:**
- `Pages/Login.razor` (Admin Portal)
- `Pages/LockScreenConfig.razor` (Admin Portal)
- Registration Page (`/register/{token}`)
- MAUI `LockScreenPage.xaml`

**Z-pattern rules applied to each screen:**

| Screen | Top-Left (Z start) | Center (diagonal) | Bottom-Right (Z end / CTA) |
|---|---|---|---|
| Login | Logo / app name | Login form (username, password) | Sign In button |
| Lock Screen Config | Section heading + device selector | Preview card (current background/logo/title) | Save / Update button |
| Registration Page | KeyGate logo | Pre-filled individual info + confirmation prompt | Register / Confirm button |
| MAUI Lock Screen | Logo (top-left of screen) | Title text + branding (visual center) | Key input field + Unlock button (bottom-right band) |

**Z-pattern rules applied everywhere:**
- **Logo/brand mark always top-left** — first thing the eye catches in the
  horizontal sweep.
- **Primary CTA bottom-right** — that's where the eye naturally lands at the
  end of the Z. Never put the primary action in the top-left or center-left.
- **Value proposition or key info in the diagonal middle** — between the two
  horizontal sweeps. This is where the Registration Page shows the person's
  details, and where the Lock Screen Config shows the visual preview.
- **Secondary actions (Cancel, Back, Logout) top-right or bottom-left** — off
  the primary Z path, visible but not competing with the main CTA.

#### 6D. Polish Pass (all components)

After layout patterns are applied, a final visual consistency pass:

- [ ] Remove any remaining unnecessary borders, divider lines, or heavy shadows
- [ ] Verify consistent spacing (multiples of the 4px base unit) across all
  pages/screens
- [ ] Verify consistent border-radius on all cards, inputs, and buttons
- [ ] Verify accent color is only on primary CTAs — no accidental colored
  labels or backgrounds
- [ ] Verify typography scale is consistent (headings, body, caption sizes
  match the token definitions)
- [ ] Test Admin Portal and Registration Page at common widths (1024px, 1280px,
  1440px) — F-pattern tables must remain scannable, Z-pattern screens must
  remain centered
- [ ] Verify MAUI Lock Screen layout reads correctly in fullscreen on a
  1920×1080 display

### Phase 7 — Self-Service Registration Flow (Week 9–10) ✅

Implement the self-service registration experience described in section 14.
This phase connects the shared registration link, mobile browser registration
form, access key generation, and desktop unlock into one seamless end-to-end
flow. Individuals fill in their own details — no admin pre-registration needed.

#### 7A. Registration Page (Razor Page — self-service, mobile-friendly)

Build the public-facing registration page that opens when an individual opens
the shared registration link on their phone.

- [x] Create Razor Page at `/register` in `KeyGate.Api/Pages/Register.cshtml`
  (blank form — individual fills in own info, no token required)
- [x] Z-pattern layout: logo top-left, form center, "Complete Registration" CTA bottom-right
- [x] Form fields: Full Name (blank), Email/Employee ID (blank), Department (blank, optional),
  Sex (dropdown), Age (number), Province, City/Municipality, Barangay,
  Sectors (checkboxes), Service Availed
- [x] On submit: `POST /api/registration/self-register` → create Individual + receive access key
- [x] Success screen: display 6-digit key in large spaced digits, copy button, one-time warning
- [x] Error states: missing fields, duplicate email/ID, network error
- [x] Mobile responsive: full-width form, 48px touch targets, appropriate mobile keyboards
- [x] Uses shared design tokens (CSS custom properties from Phase 6A)

#### 7B. API Registration Endpoints

Verify and harden the registration endpoints in
`KeyGate.Api/Controllers/RegistrationController.cs`.

- [x] `POST /api/registration/self-register` — accepts FullName, EmailOrEmployeeId, Department,
  Sex, Age, Province, CityMunicipality, Barangay, Sectors, ServiceAvailed;
  creates Individual (Status = Registered), generates 6-digit access key, BCrypt-hashes it,
  returns plain key **once**
- [x] `POST /api/registration/qr` — accepts a URL, returns QR code PNG as base64
  (used by Admin Portal for the "Share Registration Link" modal)
- [x] Access key generation: cryptographically secure 6-digit numeric key
  (`RandomNumberGenerator`), BCrypt-hashed before storage
- [x] Duplicate detection: rejects if EmailOrEmployeeId already exists in Individuals

#### 7C. Access Key Generation Service

Ensure `KeyGate.Api/Services/KeyHashingService.cs` handles access key lifecycle.

- [ ] `GenerateKey()` — returns a random 6-digit string (e.g. `"847219"`)
- [ ] `Hash(key)` — BCrypt hash for storage
- [ ] `Verify(key, hash)` — constant-time comparison via BCrypt
- [ ] Key is NEVER stored in plain text — only the hash exists in the database

#### 7D. Desktop Unlock Integration

Verify the MAUI client's unlock flow works end-to-end with a registration key.

- [ ] `KeyGate.Client/Views/LockScreenPage.xaml` — key input field accepts 6-digit numeric key
- [ ] `KeyGate.Client/ViewModels/LockScreenViewModel.cs` — `UnlockAsync()` sends
  `POST /api/sessions/unlock` with `{ key, deviceId }`
- [ ] API validates: key hash match, Individual status = Registered, Device status = Locked,
  rate limit not exceeded
- [ ] On success: Session created, Device status = Unlocked, client hides lock screen
- [ ] On failure: clear error message shown ("Invalid access key" or "Cannot reach server")

#### 7E. Admin Registration Link Sharing

Update the Admin Portal to share a general registration link instead of
per-individual QR codes.

**Individuals page** (`KeyGate.Admin/Components/Pages/Individuals.razor`):
- [x] "Share Registration Link" button in the header row (replaces "Add Individual")
- [x] QR modal opens with:
  - QR code image (generated server-side via `POST /api/registration/qr`)
  - Registration URL (e.g. `http://192.168.1.50:5000/register`)
  - Three sharing options:
    a. Print — physical QR poster for individuals to scan
    b. Copy Link — copies the URL to clipboard
    c. Share — native share dialog (with clipboard fallback)
- [x] No "Add Individual" form — individuals self-register via the shared link
- [x] Table still supports Edit and Delete per individual row

**Sharing workflow:**
1. Admin clicks "Share Registration Link" → modal opens
2. Admin shares via Print, Copy Link, or Share
3. Individual opens link → fills in own details → gets access key
4. Individual now appears in Admin's Individuals list

#### 7F. End-to-End Verification

- [x] Admin clicks "Share Registration Link" → QR modal opens with link
- [x] Phone opens registration link → self-service form loads in mobile browser
- [x] Individual fills in details → submits → access key displayed once
- [x] Individual enters key on locked desktop → desktop unlocks
- [x] Session appears in Admin Portal session logs
- [x] Idle timeout → desktop re-locks → session ends
- [x] Individual now appears in the Admin's Individuals list (status: Registered)

### Phase 8 — Individuals Table: Excel-Based Column Alignment (Week 10–11)

Align the Individuals entity, registration form, and Admin Portal table columns
to match the reference Excel file (`DTC TECH4ED.xlsx`). This ensures the digital
system tracks the same data fields as the existing paper-based sign-in sheet.

#### 8A. Excel Column Mapping

Reference: `KeyGate.Client/Excel import or export/DTC TECH4ED.xlsx`

| # | Excel Column | Current Field | Action |
|---|---|---|---|
| 1 | Seq. | `Id` | ✅ Already maps to auto-increment Id |
| 2 | Name | `FullName` | ✅ Already exists |
| 3 | Sex | — | ❌ **Add** to Individual entity + form |
| 4 | Age | — | ❌ **Add** to Individual entity + form |
| 5 | Province | — | ❌ **Add** to Individual entity + form |
| 6 | City/Municipality | — | ❌ **Add** to Individual entity + form |
| 7 | Barangay | — | ❌ **Add** to Individual entity + form |
| 8–15 | SECTOR (multi-select) | — | ❌ **Add** to Individual entity + form |
|   | → Student | — | Checkbox option |
|   | → Government Workforce | — | Checkbox option |
|   | → PWD | — | Checkbox option |
|   | → LGBTQ | — | Checkbox option |
|   | → Sr. Citizens | — | Checkbox option |
|   | → OSY (Out-of-School Youth) | — | Checkbox option |
|   | → Indigent | — | Checkbox option |
|   | → Others | — | Checkbox option |
| 16 | Service Availed | — | ❌ **Add** to Individual entity + form |
| 17 | Date | `CreatedAt` | ✅ Already maps to CreatedAt |
| 18 | Signature | — | ⏭️ **Skip** (not practical for digital self-registration) |

**Existing fields retained (system-critical, not in Excel but needed):**
- `EmailOrEmployeeId` — uniqueness constraint, still required in registration form
- `Status` — system state (Pending/Registered), still needed
- `CreatedByAdminId` — nullable, tracks admin vs self-registered
- `Department` — optional, kept for backwards compatibility (not shown in Excel columns but already in the system)

**Sector storage approach:**
- Store as a **JSON array string** in a single `Sectors` column on `Individuals`
- Example: `["Student","PWD"]` or `null`/empty for no sector selected
- Allows multi-select in the form and clean display in the Admin table

#### 8B. Individual Entity Changes

File: `KeyGate.Api/Entities/Individual.cs`

Add new nullable fields:

```csharp
public string? Sex { get; set; }               // "Male", "Female", "Other"
public int? Age { get; set; }                   // e.g. 25
public string? Province { get; set; }           // e.g. "Davao del Sur"
public string? CityMunicipality { get; set; }   // e.g. "Davao City"
public string? Barangay { get; set; }           // e.g. "Talomo"
public string? Sectors { get; set; }            // JSON array: ["Student","PWD"]
public string? ServiceAvailed { get; set; }     // e.g. "Computer Usage"
```

All fields nullable — existing records remain valid without data.

#### 8C. API DTO Changes

**IndividualsController.cs** — update DTOs:
- `IndividualDto` — add Sex, Age, Province, CityMunicipality, Barangay, Sectors, ServiceAvailed
- `UpdateIndividualRequest` — add the same fields for edit support
- `SelfRegisterRequest` in RegistrationController — add Sex, Age, Province, CityMunicipality, Barangay, Sectors, ServiceAvailed

**IndividualsController.cs** — update GET/PUT mappings to include new fields.

#### 8D. Registration Page Form Update

File: `KeyGate.Api/Pages/Register.cshtml` + `Register.cshtml.cs`

Update the self-service form to collect the new fields:
- Sex (dropdown: Male / Female / Other)
- Age (number input)
- Province (text input)
- City/Municipality (text input)
- Barangay (text input)
- Sectors (checkboxes for the 8 sector options)
- Service Availed (text input or dropdown)

Keep existing fields:
- Full Name (required)
- Email / Employee ID (required)
- Department (optional, kept for backwards compatibility)

Z-pattern layout maintained — form fields in diagonal center, CTA bottom-right.

#### 8E. Admin Portal Individuals Table Update

File: `KeyGate.Admin/Components/Pages/Individuals.razor`

Replace current columns with Excel-aligned columns:

**Before (current):**
```
[Name] [Status] [Department] [Email/ID] [Created] [Actions]
```

**After (Excel-aligned, F-pattern):**
```
[Seq/Id] [Name] [Sex] [Age] [Province] [City/Municipality] [Barangay] [Sectors] [Service Availed] [Date] [Actions]
```

- F-pattern: left-load columns, most important (Name, Status) in first positions
- Sectors column: display as badges/chips (e.g. `Student`, `PWD`, etc.)
- Email/ID, Department hidden from default table view (still available in edit modal)
- Search still works across all fields

#### 8F. Admin Models (AdminApiClient.cs + AdminModels.cs) Update

File: `KeyGate.Admin/Models/AdminModels.cs`
- Update `IndividualDto` record with new fields
- Update `UpdateIndividualRequest` record with new fields

File: `KeyGate.Admin/Services/AdminApiClient.cs`
- Update `UpdateIndividualAsync` method signature to include new fields

#### 8G. EF Migration

Create migration `AddExcelFieldsToIndividual` to add the new nullable columns.

#### 8H. Implementation Checklist

- [ ] Update `Individual.cs` entity with 7 new fields
- [ ] Create EF migration `AddExcelFieldsToIndividual`
- [ ] Update `IndividualsController.cs` DTOs (IndividualDto, UpdateIndividualRequest)
- [ ] Update `RegistrationController.cs` DTOs (SelfRegisterRequest) + SelfRegister logic
- [ ] Update `Register.cshtml` form (add Sex, Age, Province, City/Municipality, Barangay, Sectors, Service Availed fields)
- [ ] Update `Register.cshtml.cs` (add bindings for new fields)
- [ ] Update `AdminModels.cs` (IndividualDto, UpdateIndividualRequest)
- [ ] Update `AdminApiClient.cs` (UpdateIndividualAsync signature)
- [ ] Update `Individuals.razor` table columns to match Excel
- [ ] Update `Individuals.razor` edit modal to include new fields
- [ ] Update `Individuals.razor` search filter to include new fields
- [ ] Build all 3 projects (0 errors)
- [ ] Cross-check: verify every Excel column has a matching system field
- [ ] F/Z pattern layout reviewed and applied

#### 8I. Verification — Excel Cross-Check

Final verification against the Excel file:

| Excel Column | System Field | Entity | Registration Form | Admin Table | Status |
|---|---|---|---|---|---|
| Seq. | Id | ✅ | — (auto) | ✅ | ⬜ |
| Name | FullName | ✅ | ✅ | ✅ | ⬜ |
| Sex | Sex | ✅ | ✅ | ✅ | ⬜ |
| Age | Age | ✅ | ✅ | ✅ | ⬜ |
| Province | Province | ✅ | ✅ | ✅ | ⬜ |
| City/Municipality | CityMunicipality | ✅ | ✅ | ✅ | ⬜ |
| Barangay | Barangay | ✅ | ✅ | ✅ | ⬜ |
| Student (sector) | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| Government Workforce | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| PWD | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| LGBTQ | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| Sr. Citizens | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| OSY | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| Indigent | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| Others | Sectors (JSON) | ✅ | ✅ | ✅ | ⬜ |
| Service Availed | ServiceAvailed | ✅ | ✅ | ✅ | ⬜ |
| Date | CreatedAt | ✅ | — (auto) | ✅ | ⬜ |
| Signature | — | ⏭️ | ⏭️ | ⏭️ | N/A |

---

## 13. Future Enhancements (optional, post-MVP)

- Push notifications to admin when a device is left unlocked too long
- Facial recognition or NFC card as an alternative to typing the key
- Per-department lock screen branding
- Analytics dashboard (usage per individual, per device, peak hours)

---

## 14. Self-Service Registration Flow — Full Feature Spec

This section consolidates and expands the end-to-end self-service registration
experience described across sections 2, 7.2, and 8B. It serves as the single
reference for how an individual goes from opening a shared registration link to
having an Access Key that unlocks a desktop. Individuals fill in their own
details — no admin pre-registration required.

### 14.1 End-to-End Flow Overview

```
[Admin Portal]
  1. Admin clicks "Share Registration Link" on the Individuals page
  2. QR modal opens with registration URL: http://{API_HOST}/register
  3. Admin shares the link via:
     a. Print — physical QR poster for individuals to scan
     b. Copy Link — copies the URL to clipboard (paste into chat/email/SMS)
     c. Share — native share dialog (WhatsApp, email, etc.)

[Individual receives the link]
  4. Individual either:
     a. Scans the printed QR code with phone camera, OR
     b. Opens the shared URL directly in their phone's browser
  5. Phone opens the Registration Page in the default mobile browser
     URL: http://{API_HOST}/register
  6. Registration Page loads:
     a. Displays a blank form with fields: Full Name, Email/Employee ID, Department,
        Sex, Age, Province, City/Municipality, Barangay, Sectors, Service Availed
     b. No pre-filled data — the individual enters their own information
  7. Individual fills in their details:
     - Full Name (required)
     - Email or Employee ID (required)
     - Department (optional)
     - Sex (required — Male / Female / Other)
     - Age (required — number)
     - Province (required)
     - City/Municipality (required)
     - Barangay (required)
     - Sectors (optional — checkboxes: Student, Government Workforce, PWD, LGBTQ,
       Sr. Citizens, OSY, Indigent, Others)
     - Service Availed (required)
  8. Individual taps "Complete Registration"
  9. Browser sends POST /api/registration/self-register with the form data
 10. API:
     a. Creates a new Individual record (Status = Registered)
     b. Generates a random 6-digit Access Key
     c. BCrypt-hashes the key and stores it in the AccessKeys table
     d. Returns the plain-text Access Key to the browser (one-time only)
 11. Registration Page displays:
     ┌─────────────────────────────────────────┐
     │  ✅ Registration Complete!               │
     │                                         │
     │  Your Access Key:  8 4 7 2 1 9          │
     │                                         │
     │  ⚠️ Save this key now. It will NOT be   │
     │  shown again.                           │
     │                                         │
     │  [Copy Key]                              │
     └─────────────────────────────────────────┘
 12. Individual copies/saves the key

[Desktop / Computer — KeyGate.Client.exe]
 13. Individual walks to any locked desktop running the client
 14. Enters the 6-digit Access Key into the lock screen input field
 15. Client sends POST /api/sessions/unlock with { key, deviceId }
 16. API validates:
     a. Key hash matches an existing AccessKey
     b. Associated Individual status is "Registered"
     c. Device is currently Locked (not already occupied)
     d. Rate limit not exceeded for this device/IP
 17. If valid:
     a. Creates a Session record (IndividualId, DeviceId, StartedAt)
     b. Sets Device.Status = Unlocked (atomic transaction)
     c. Returns session info (sessionId, individualName)
 18. Client hides the lock screen → desktop is usable
 19. Session is tracked until: manual lock, idle timeout, or forced lock by admin
```

### 14.2 Registration Page — UI Specification

**Pattern:** Z-pattern (single-action form on a visual layout)

**Layout (Z-pattern):**
| Z Position | Content |
|---|---|
| Top-left (Z start) | KeyGate logo + "Register" heading |
| Center (diagonal) | Blank form fields: Full Name, Email/Employee ID, Department |
| Bottom-right (Z end / CTA) | "Complete Registration" button |

**Form Fields:**

| Field | Source | Editable? | Validation |
|---|---|---|---|
| Full Name | Individual enters their own name | Yes | Required, min 2 characters |
| Email or Employee ID | Individual enters their own email/ID | Yes | Required, must match format (email or alphanumeric ID) |
| Department | Individual enters their department | Yes | Optional |
| Sex | Individual selects from dropdown | Yes | Required (Male / Female / Other) |
| Age | Individual enters their age | Yes | Required, number 1–150 |
| Province | Individual enters their province | Yes | Required |
| City/Municipality | Individual enters their city/municipality | Yes | Required |
| Barangay | Individual enters their barangay | Yes | Required |
| Sectors | Individual selects one or more checkboxes | Yes | Optional (Student, Government Workforce, PWD, LGBTQ, Sr. Citizens, OSY, Indigent, Others) |
| Service Availed | Individual enters or selects service | Yes | Required |

**States:**

1. **Form** — blank form with "Complete Registration" button
2. **Success** — access key revealed, copy button, warning about one-time display
3. **Error — Missing Fields** — "Full name and Email / Employee ID are required."
4. **Error — Duplicate** — "An individual with that email/ID already exists."
5. **Error — Network** — "Cannot reach the server. Please check your WiFi connection."

**Mobile Responsiveness:**
- Full-width form on phones (< 640px)
- Single-column layout, large touch targets (48px minimum)
- Input fields use appropriate mobile keyboards (email keyboard for email field)
- "Complete Registration" button full-width on mobile

### 14.3 Access Key — Generation & Display

**Generation:**
- System generates a random 6-digit numeric key (e.g. `847219`)
- Key is generated server-side using a cryptographically secure RNG
- Key is BCrypt-hashed before storage — plain text is NEVER persisted

**Display:**
- Key is shown **exactly once** on the success screen after registration
- Key is displayed in large, spaced digits for easy reading: `8 4 7 2 1 9`
- A "Copy to Clipboard" button is provided for convenience
- A warning message is shown: "Save this key now. It will NOT be shown again."
- The individual must physically write down, screenshot, or memorize the key
  before leaving the page

**Security:**
- The plain key is included only in the single API response — it is never
  stored, logged, or recoverable
- If the individual loses the key, they can re-open the registration link and
  register again (must use a different email/ID, or admin deletes the old record)

### 14.4 Desktop Unlock — Using the Access Key

**Lock Screen (Z-pattern layout):**
| Z Position | Content |
|---|---|
| Top-left | KeyGate logo |
| Center (diagonal) | Title text + subtitle ("Enter your access key to unlock") |
| Bottom-right | Access key input field + Unlock button |

**Unlock flow:**
1. Individual types the 6-digit key into the input field
2. Taps "Unlock" (or presses Enter)
3. Client sends `POST /api/sessions/unlock` with `{ key, deviceId }`
4. On success: lock screen hides, welcome message shown, session starts
5. On failure: error message displayed ("Invalid access key" or "Cannot reach server")

**Key validation rules (enforced by API):**
- Key must match a BCrypt hash in the AccessKeys table
- Associated Individual must have Status = `Registered`
- Target device must be currently `Locked` (not already in use)
- Rate limit: max 5 unlock attempts per minute per device (HTTP 429 on breach)
- The same key cannot unlock two devices simultaneously — if the individual is
  already unlocked on another device, the API rejects the request

### 14.5 API Endpoints for Registration Flow

```
Registration (public — self-service, no auth required)
  POST /api/registration/self-register   → individual fills in own info, creates record + access key
  POST /api/registration/qr              → generates QR code PNG for a given URL

Registration (legacy — token-gated, still available for admin pre-registration)
  GET  /api/registration/{token}          → validates token, returns pre-filled data
  POST /api/registration/{token}/complete  → completes registration, returns access key

Individuals (admin only — for management after self-registration)
  GET    /api/individuals                 → list all registered individuals
  PUT    /api/individuals/{id}            → edit individual details
  DELETE /api/individuals/{id}            → remove an individual

Unlock (device-authenticated)
  POST   /api/sessions/unlock             → validates key, creates session, unlocks device
```

### 14.6 Database Records Created During Registration

| Step | Table | Record Created/Updated |
|---|---|---|
| Individual self-registers | `Individuals` | New row (Status = `Registered`, CreatedByAdminId = null) |
| Individual self-registers | `AccessKeys` | New row (KeyHash = BCrypt hash, IsActive = true) |
| Individual unlocks desktop | `Sessions` | New row (IndividualId, DeviceId, StartedAt) |
| Individual unlocks desktop | `Devices` | Updated: `Status = Unlocked` |

### 14.7 Network Requirement

The Registration Page is hosted on the **Backend API** (same server). Since the
API is on the local LAN (section 6.6), the individual's phone **must be
connected to the same WiFi network** as the host machine to:

1. Open the registration URL from the QR code
2. Submit the registration form
3. Receive the access key

This is by design — only people physically on the network can self-register.
If the phone is on cellular data, the registration page will not load.

### 14.8 Error Recovery

| Scenario | Recovery |
|---|---|
| Individual loses the access key | Individual re-opens the registration link → fills in details again (must use a different email/ID, or admin deletes the old record first) |
| Individual registered but key doesn't work | Verify key is correct, device is locked, and server is reachable; admin can check session logs |
| Phone can't reach registration page | Phone must be on the same WiFi as the host machine (section 6.6) |
| Duplicate email/ID | System rejects with "An individual with that email/ID already exists." — admin can edit/delete the existing record if needed |

### 14.9 Z-Pattern Checklist for Registration Page

- [ ] F/Z pattern layout reviewed and applied
- [ ] Logo/brand mark top-left
- [ ] Form fields in the diagonal center
- [ ] "Complete Registration" CTA bottom-right
- [ ] Success screen: access key prominently displayed, copy button, one-time warning
- [ ] Error states: clear messages for expired, used, invalid, and network errors
- [ ] Mobile responsive: full-width form, large touch targets, appropriate keyboards
- [ ] Uses shared design tokens (typography, color, spacing, border-radius)

### 14.10 QR Code Sharing — Admin Portal

The admin shares a **general registration link** with individuals via the QR
modal in the Individuals page. Three sharing methods are available:

| Method | How it works | Best for |
|---|---|---|
| **Print** | Opens browser print dialog with the QR code formatted for paper | Physical posters, in-person handout |
| **Copy Link** | Copies the registration URL to clipboard; admin pastes it into any message | Chat apps (Messenger, Viber), email, SMS |
| **Share** | Triggers the Web Share API (`navigator.share`) to open the device's native share sheet | Mobile admin devices, quick sharing to WhatsApp/email/SMS |

**QR Modal layout:**
```
┌─────────────────────────────────────────┐
│  Share Registration Link                │  ← Header
├─────────────────────────────────────────┤
│                                         │
│  Share this link with individuals to    │  ← Description
│  let them register themselves.          │
│                                         │
│         ┌───────────────┐               │
│         │   QR CODE     │               │  ← 220×220px QR image
│         │   (PNG)       │               │     (encodes the registration URL)
│         └───────────────┘               │
│                                         │
│  http://192.168.1.50:5000/register      │  ← Registration URL (text)
│                                         │
│  [ Print ]  [ Copy Link ]  [ Share ]    │  ← Three action buttons
│                                         │
│  Link copied!                           │  ← Temporary confirmation (fades)
└─────────────────────────────────────────┘
```

**Fallback behavior:**
- If the browser doesn't support the Web Share API (most desktop browsers),
  "Share" falls back to clipboard copy with a message: "Link copied! (Share not
  supported on this browser)"
- If clipboard API is unavailable, a manual text selection approach is used

**Sharing workflow (end-to-end):**
1. Admin clicks "Share Registration Link" on the Individuals page → QR modal opens
2. Admin chooses a sharing method:
   - **Print** → individual scans the printed QR with their phone
   - **Copy Link** → admin pastes the URL into a chat message → individual taps the link
   - **Share** → native share sheet opens → individual selects WhatsApp/email/SMS
3. Individual's phone opens the Registration Page at the shared URL
4. Individual fills out the form → receives access key
5. Individual uses the key to unlock a desktop
6. Individual now appears in the Admin's Individuals list

---

## 15. Frontend Design Pattern Rule (applies to all future work)

Every new feature, page, or screen added to the KeyGate project **must** include
a design review sub-task before it is marked complete. This rule exists to
ensure F-pattern and Z-pattern compliance is built in from the start, not
retrofitted later.

**When adding a new UI feature, the implementation plan must specify:**

1. **Pattern classification** — is this page/screen **text/data-heavy
   (F-pattern)** or **visual/action-focused (Z-pattern)**? Classify based on
   what the user is doing: scanning rows of data = F; taking a single action on
   a visual layout = Z.

2. **Layout must follow the corresponding pattern rules** (from Phase 6):
   - **F-pattern**: front-load key words in headings, left-align navigation and
     content edges, left-load data columns, no critical info in middle-right.
   - **Z-pattern**: logo/brand top-left, primary CTA bottom-right, value prop or
     key info in the diagonal middle, secondary actions off the Z path.

3. **Design token compliance** — the new UI must use the shared design tokens
   (typography, color, spacing, border-radius, shadows) defined in Phase 6A.
   No ad-hoc visual values.

4. **Checklist item** — the new feature's task list must include a checkbox:
   `⬜ F/Z pattern layout reviewed and applied` — this must be checked before
   the feature is considered done.

**Quick reference — which pattern to use:**

| Page type | Pattern | Examples in KeyGate |
|---|---|---|
| Data table / list view | F-pattern | Session logs, Individuals list, Devices list |
| Dashboard with stat cards | F-pattern (primary scan) + Z hybrid | Admin live dashboard |
| Login / auth screen | Z-pattern | Admin login |
| Single-action form | Z-pattern | Registration page, Lock Screen Config |
| Full-screen visual with one CTA | Z-pattern | MAUI Lock Screen |
| Settings page with sections | F-pattern (section headings) + Z (save CTA) | Any future settings |

This section is **not optional** and applies retroactively to existing pages
during Phase 6, and proactively to every new feature thereafter.


You must follow ONLY the instructions and structure written in the attached
file: kiosk-lock-screen-dev-plan.md

Rules:
1. Treat this md file as the single source of truth for the KeyGate project.
2. Do not add features, sections, or files that are not described in it.
3. If my request is unclear, unrelated to the md file, or looks like a typo
   or wrong word (I'm not a native English speaker, so this may happen),
   do NOT guess and do NOT change unrelated parts of the project.
   Instead, ask me to confirm exactly what I meant before doing anything.
4. Only modify the specific file, section, or code I point to. Never
   touch, refactor, rename, or "clean up" other files unless I explicitly
   say so.
5. If a request conflicts with something already decided in the md file
   (e.g. LAN-only hosting, PostgreSQL, KeyGate naming), point out the
   conflict first instead of silently changing the plan.
6. Before making any change, briefly restate what you understood I asked
   for, in plain terms, so I can correct it if it's wrong.