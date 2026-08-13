# KeyGate — Kiosk Access & Lock Screen Management System

A centrally-managed lock screen system for shared/kiosk computers. Each desktop
runs a **.NET MAUI lock screen client**. Individuals are pre-registered by an
**administrator**, then complete self-registration by **scanning a QR code**,
which gives them a personal **access key**. That key unlocks **any** available
(not-yet-unlocked) computer running the client. Every unlock/lock event is
logged as a session.

> Full development plan: [`KeyGate.Client/MD/kiosk-lock-screen-dev-plan.md`](KeyGate.Client/MD/kiosk-lock-screen-dev-plan.md)

**Core idea:** Admin registers people → system generates a QR/token per person →
person scans QR → completes registration → receives a key → uses that key on
any locked desktop client → desktop unlocks and logs the session.

---

## Components

| Component | Project | Output |
|---|---|---|
| Backend API | `KeyGate.Api` | ASP.NET Core Web API (hosted, not an exe) |
| MAUI Desktop Lock Screen Client | `KeyGate.Client` | `KeyGate.Client.exe` — the app running on each shared computer |
| Admin Portal | `KeyGate.Admin` | Blazor Server web app (hosted, not an exe) |
| Shared Models/DTOs | `KeyGate.Shared` | Class library referenced by Api + Client *(planned, not created yet)* |

`KeyGate.Client.exe` is the file installed/auto-started on every kiosk computer —
it is the actual lock screen application.

---

## Core User Flow

1. Admin logs in to the Admin Portal.
2. Admin pre-registers an individual (name, email/ID, department).
3. The system generates a unique **Registration Token + QR code** for that person.
4. Admin shares/prints/displays the QR code.
5. The individual scans the QR code with their phone (on the same WiFi/LAN).
6. A mobile-friendly **Registration Page** opens, pre-filled with their info.
7. The individual confirms their details and is issued a one-time access key.
8. On a kiosk computer, the lock screen shows and the user enters their key.
9. The client validates the key against the backend API.
10. If valid **and** not already active on another unlocked machine, the lock
    screen hides and a **Session** starts.
11. On logout / idle timeout / manual re-lock, the session closes and the lock
    screen reappears.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Desktop lock screen | .NET MAUI (Windows target via WinUI head), MVVM |
| Backend API | ASP.NET Core Web API (.NET 9), EF Core |
| Database | PostgreSQL (central, single source of truth) |
| Admin Portal | Blazor Server (.NET 9) |
| Registration Page | Razor Page hosted inside `KeyGate.Api` at `/register/{token}` |
| QR generation | QRCoder (server-side) |
| Real-time device status | SignalR hub at `/hubs/devices` |
| Admin auth | JWT (issued by the API), cookie session in the Admin Portal |
| End-user key auth | Custom hashed key lookup (BCrypt) |
| Hosting | Local always-on host machine on your LAN |

---

## Repository Structure

```
KeyGate.Client.sln
├── KeyGate.Api/            Backend API (controllers, entities, EF Core, SignalR hub, Razor registration page)
├── KeyGate.Admin/          Blazor Server admin portal (login, individuals, devices, lock screen, sessions, dashboard)
├── KeyGate.Client/         .NET MAUI lock screen client (MVVM, SQLite cache, kiosk fullscreen)
│   └── MD/                 Development plan document
└── GITHUB-COMMANDS.md      Git/GitHub command cheat sheet
```

---

## Getting Started

### Prerequisites

- .NET 9 SDK (the solution targets `net9.0`)
- PostgreSQL (14+ recommended) — see the plan section 6.5 for install options
- For the MAUI client: the .NET MAUI workload (Windows-only builds work on Windows)

### 1. Database setup

Create the database and a dedicated app user (don't use the `postgres` superuser
from the API):

```sql
CREATE DATABASE keygate_db;
CREATE USER keygate_app WITH ENCRYPTED PASSWORD 'strong-password-here';
GRANT ALL PRIVILEGES ON DATABASE keygate_db TO keygate_app;
```

### 2. Backend API (`KeyGate.Api`)

Set the connection string in `KeyGate.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "KeyGateDb": "Host=localhost;Port=5432;Database=keygate_db;Username=keygate_app;Password=strong-password-here"
}
```

Create and apply the schema:

```bash
dotnet ef database update --project KeyGate.Api
```

Seed a default admin account (used by the Admin Portal) in `appsettings.json`:

```json
"AdminSeed": {
  "FullName": "KeyGate Administrator",
  "Email": "admin@keygate.local",
  "Password": "ChangeMe123!",
  "Role": "Admin"
}
```

> The seed runs once on API startup. Never commit real passwords — this is a dev seed.

Run the API:

```bash
dotnet run --project KeyGate.Api
# http://localhost:5000

# HTTPS in dev (HTTP on :5000 will redirect to HTTPS):
dotnet run --project KeyGate.Api --launch-profile https
# https://localhost:7000  (dev cert already trusted via `dotnet dev-certs https --trust`)
```

### 3. Admin Portal (`KeyGate.Admin`)

Point the portal at the API in `KeyGate.Admin/appsettings.json`:

```json
"KeyGateApi": {
  "BaseUrl": "http://localhost:5000"
}
```

Run the portal:

```bash
dotnet run --project KeyGate.Admin
# http://localhost:5213  ->  sign in with the seeded admin account
```

### 4. MAUI Lock Screen Client (`KeyGate.Client`)

Set the API address in `KeyGate.Client/appsettings.json`:

```json
"AppSettings": {
  "ApiBaseUrl": "http://localhost:5000",
  "DeviceNamePrefix": "Kiosk",
  "IdleTimeoutMinutes": 5,
  "ConfigRefreshMinutes": 10
}
```

Build/run on Windows:

```bash
dotnet build KeyGate.Client/KeyGate.Client.csproj -t:Run -f net9.0-windows10.0.19041.0
```

On first run the client self-registers with the API (obtaining a device ID +
API key), then shows the lock screen.

---

## LAN-Only Deployment (no internet required)

"Centralized" means one shared database — it does **not** require internet
hosting. PostgreSQL and `KeyGate.Api` can both live on a single machine on your
WiFi/LAN; every other component just needs to be on that same network.

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

Key steps (details in plan section 6.6):

1. Install PostgreSQL + `KeyGate.Api` on the always-on host machine (wired Ethernet preferred).
2. Set a **DHCP reservation** so the host's IP never changes.
3. Configure the firewall to allow inbound traffic on the API port from the local subnet only.
4. Point every `KeyGate.Client.exe` and the Admin Portal at the host's local IP, e.g. `http://192.168.1.50:5000`.
5. QR codes encode that local URL, so phones must be on the same WiFi to self-register.
6. HTTPS is optional on a closed LAN (no public exposure); a self-signed certificate is enough if you want it.

---

## API Endpoints (draft)

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
  POST   /api/lockscreen-config/upload   (background/logo image upload)

Sessions
  POST   /api/sessions/unlock
  POST   /api/sessions/{id}/end
  GET    /api/sessions                (admin, filterable)

Real-time (SignalR hub)
  /hubs/devices   -> broadcasts DeviceStatusChanged events
```

---

## Security Notes

- Access keys are **never stored in plain text** — BCrypt-hashed in the database.
- Registration tokens expire (default 48h) and are single-use.
- Unlock + session-start happen as **one atomic database transaction**, so the
  same key can't unlock two computers at the same instant.
- The unlock endpoint is rate-limited (5 attempts/min/device, HTTP 429) to blunt key brute-forcing.
- Each MAUI client authenticates with a device credential issued at first-run registration.
- One active session per key (a key already active on another device is rejected).
- Session logs are immutable (no hard deletes, only status flags).
- Desktops never touch the database directly — everything goes through the API.

---

## Project Status

| Phase | Scope | Status |
|---|---|---|
| 1 | Backend foundation (API, entities, admin auth, individuals CRUD + QR) | ✅ Done |
| 2 | Registration flow (Razor page, key generation + one-time reveal) | ✅ Done |
| 3 | MAUI lock screen client (fullscreen UI, device self-registration, unlock, idle timeout, SQLite cache) | ✅ Done |
| 4 | Admin Portal (login, individuals, devices, lock screen customization, session logs + CSV, SignalR live dashboard) | ✅ Done |
| 5 | Hardening & deployment (rate limiting, HTTPS in dev, Windows auto-start; LAN host setup + pilot documented as a manual runbook) | ✅ Code done (ops runbook in plan) |

See the development plan for the full roadmap and future enhancements.
