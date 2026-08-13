# KeyGate — Git / GitHub Commands

Cheat sheet for using this repository manually. Run these in **Git Bash**, **PowerShell**, or **CMD** from the project folder:

```text
C:\Users\John Lloyd T. Caban\Documents\GitHub\KeyGate.Client
```

---

## 1. First-time setup

```bash
# Clone the repository (new machine / fresh copy)
git clone https://github.com/Johnlloyd17/KeyGate.Client.git
cd KeyGate.Client

# See the remote repository (should point to GitHub)
git remote -v
```

---

## 2. Daily workflow (save + upload your changes)

```bash
# 1) Check what changed
git status

# 2) See exactly what's different in each file
git diff

# 3) Stage the files you want to save
git add .                          # stage everything
git add KeyGate.Api/Program.cs     # stage a single file
git add KeyGate.Client/Views/      # stage a folder

# 4) Commit with a message
git commit -m "Describe what you changed"

# 5) Upload to GitHub
git push
```

---

## 3. Getting updates from GitHub

```bash
# Download the latest changes from the repo
git pull

# Pull with rebase (keeps history cleaner)
git pull --rebase

# See the last 10 commits
git log --oneline -10
```

---

## 4. Fixing mistakes / undoing

```bash
# Unstage a file (keep the changes in the working folder)
git reset HEAD file.cs

# Discard changes in a file (permanent!)
git checkout -- file.cs

# Amend the last commit message
git commit --amend -m "Better message"

# Undo last commit but keep changes
git reset --soft HEAD~1
```

---

## 5. Branches

```bash
# List branches
git branch

# Create and switch to a new branch
git checkout -b feature/my-change

# Switch back to master
git checkout master

# Push a new branch to GitHub
git push -u origin feature/my-change
```

---

## 6. GitHub CLI (`gh`)

Login once (browser device-code flow):

```bash
gh auth login
```

Common commands:

```bash
gh auth status                 # who am I logged in as
gh repo view                   # open repo page info
gh repo view --web             # open repo in browser
gh pr create                   # create a pull request
gh pr list                     # list open PRs
gh run list                    # list CI runs (if workflows added)
```

---

## 7. Building / running the projects

```bash
# Build everything in the solution
dotnet build KeyGate.Client.sln

# Run the API (http://localhost:5000)
dotnet run --project KeyGate.Api

# Run the Blazor admin web app
dotnet run --project KeyGate.Admin

# Run the MAUI client on Windows
dotnet build KeyGate.Client/KeyGate.Client.csproj -t:Run -f net9.0-windows10.0.19041.0
```

> MAUI targets: `net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`,
> `net9.0-windows10.0.19041.0` (Windows only on Windows machines).

---

## 8. Database migrations (API)

```bash
# Add a new migration after changing entities
dotnet ef migrations add <MigrationName> --project KeyGate.Api

# Apply migrations to the local database
dotnet ef database update --project KeyGate.Api
```

---

## Quick reference

| I want to...                       | Command                          |
| ---------------------------------- | -------------------------------- |
| See what changed                   | `git status`                     |
| Stage everything                   | `git add .`                      |
| Save with a message                | `git commit -m "msg"`            |
| Upload to GitHub                   | `git push`                       |
| Download latest                    | `git pull`                       |
| See history                        | `git log --oneline -10`          |
| New branch                         | `git checkout -b <name>`         |
| Log into gh                        | `gh auth login`                  |
| Open repo in browser               | `gh repo view --web`             |
