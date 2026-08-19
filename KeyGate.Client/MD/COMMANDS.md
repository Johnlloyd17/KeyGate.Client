# KeyGate — Quick Start & Command Reference

---

## Prerequisites

- .NET 10 SDK (`dotnet --list-sdks`)
- PostgreSQL running (service `postgresql-x64-18` on port 5432)
- Update `KeyGate.Api/appsettings.json` → set your real PostgreSQL password in `ConnectionStrings.KeyGateDb`

---

## Starting All 3 Projects

Open **3 separate terminals** and run each command:

### Terminal 1 — Backend API
```bash
cd KeyGate.Api
dotnet run
```
- Runs at `http://localhost:5000` (HTTP) or `https://localhost:7000` (HTTPS)
- On startup: auto-creates database, runs migrations, seeds admin user
- Watch console for: `Seeded default admin 'admin@keygate.local'.`

### Terminal 2 — Admin Portal
```bash
cd KeyGate.Admin
dotnet run --launch-profile https
```
- Runs at `https://localhost:7137`
- Login: `admin@keygate.local` / `ChangeMe123!`

### Terminal 3 — MAUI Desktop Client (Lock Screen)
```bash
cd KeyGate.Client
dotnet run -f net10.0-windows10.0.19041.0
```
- Runs fullscreen as a lock screen on the desktop
- Auto-registers itself with the API on first run

---

## Build Commands

```bash
# Build everything
dotnet build

# Build specific project
dotnet build KeyGate.Api/KeyGate.Api.csproj
dotnet build KeyGate.Admin/KeyGate.Admin.csproj
dotnet build KeyGate.Client/KeyGate.Client.csproj -f net10.0-windows10.0.19041.0

# Restore packages
dotnet restore
```

---

## Database Commands (EF Core)

```bash
cd KeyGate.Api

# Add a migration after changing entities
dotnet ef migrations add MigrationName

# Apply pending migrations (also auto-runs on API startup)
dotnet ef database update

# List migrations
dotnet ef migrations list

# Remove last migration (only if not applied yet)
dotnet ef migrations remove
```

---

## Admin Login

| Field | Value |
|---|---|
| URL | `https://localhost:7137` |
| Email | `admin@keygate.local` |
| Password | `ChangeMe123!` |

---

## API Endpoints

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
  POST   /api/devices/register
  PUT    /api/devices/{id}

Lock Screen Config
  GET    /api/lockscreen-config?deviceId=
  POST   /api/lockscreen-config

Sessions
  POST   /api/sessions/unlock
  POST   /api/sessions/{id}/end
  GET    /api/sessions

Real-time (SignalR)
  /hubs/devices
```

---

## Port Reference

| Service | HTTP | HTTPS |
|---|---|---|
| API | `http://localhost:5000` | `https://localhost:7000` |
| Admin Portal | `http://localhost:5213` | `https://localhost:7137` |
| MAUI Client | — | — (desktop app) |

---

## Project Structure

```
KeyGate.Api/          → Backend API (ASP.NET Core)
KeyGate.Admin/        → Admin Portal (Blazor Server)
KeyGate.Client/       → Desktop Lock Screen (.NET MAUI)
KeyGate.Shared/       → Shared DTOs/Models (class library)
KeyGate.Client/MD/    → This project plan & docs
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| API: `Password authentication failed` | Wrong PostgreSQL password in `KeyGate.Api/appsettings.json` |
| API: `relation "Admins" does not exist` | Migrations not applied — API auto-runs them on startup, restart the API |
| Admin: `Invalid email or password` | API console will show why — check for `Login failed: no admin found` or `wrong password` |
| Admin: `Cannot reach the KeyGate API` | Make sure API is running on `http://localhost:5000` |
| MAUI: connection error on startup | Make sure API is running first, then start the client |
| MAUI: app doesn't go fullscreen | Windows may block fullscreen — run as administrator |
