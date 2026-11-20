# CrisisAid 2026 — Safe upgrade of the EXISTING Windows installation

This guide upgrades the existing React + ASP.NET + SQL Server project **without destroying the existing CrisisAid database**. It assumes the current folder is `C:\Project\Humanitarian_Aid_Platform`, SQL Server runs through Docker Desktop, and the backend container is called `api`.

## A. Before changing anything

1. Open Docker Desktop. Check **Engine running**.
2. Close the tab or terminal currently running `npm.cmd run dev` using **Ctrl+C**.
3. In Windows File Explorer make a folder `C:\Project\CrisisAid_Backup`.
4. Copy the original `frontend` folder into that backup folder. Also back up `backend`, `docker-compose.yml`, and your **private** `.env` file. Do not put passwords in a public Git repo.
5. Optional but recommended: back up SQL Server using SSMS before changing the backend. **Do not use** `docker compose down -v` — it destroys the database volume.

## B. Apply the upgrade ZIP (recommended — preserves your old docs)

1. Download `CrisisAid_Upgrade_Overlay.zip`.
2. Right-click the ZIP > **Extract All** into a temporary folder such as `C:\Project\CrisisAid_Update`.
3. Inside the extracted folder, open `Humanitarian_Aid_Platform`.
4. Select `backend`, `frontend`, `docker-compose.yml`, and `.env.example`, and copy them.
5. Paste into **your existing** `C:\Project\Humanitarian_Aid_Platform` directory.
6. Select **Replace files in destination** when Windows asks. This applies changed source code while leaving all unrelated files and your existing `.env` intact.
7. Do not replace your real `.env` file with `.env.example`.

Alternatively, extract `CrisisAid_Expanded_Full_Project.zip` to a **new directory** for a clean copy. Do not point a new Docker project name at your existing volume without verifying database settings first.

## C. Check passwords and frontend port

Open the existing root `.env`. It needs `SA_PASSWORD`, `JWT_KEY` (at least 32 bytes), and `DEMO_PASSWORD` (at least 12 characters). **Keep the working existing SQL Server password**; changing `SA_PASSWORD` in `.env` does not change the password in an initialised database.

Add this line to `.env` (don't delete the existing lines):

```dotenv
FRONTEND_URL=http://localhost:5173
```

Use `http://localhost:5174` if you plan to run the frontend on 5174 instead.

To choose the port explicitly, in `frontend` copy `.env.example` to `.env.local`:

```powershell
cd C:\Project\Humanitarian_Aid_Platform\frontend
Copy-Item .env.example .env.local
```

Edit `VITE_FRONTEND_PORT=5173` to `5174` if needed. The frontend has `strictPort: true` to avoid silent switches. Both frontend port and `FRONTEND_URL` in the root `.env` must match for Google sign-in redirects. Your Google OAuth authorised redirect URI remains `http://localhost:8080/api/auth/google/callback`.

## D. Build and launch the API safely

In VS Code open **Terminal > New Terminal**:

```powershell
cd C:\Project\Humanitarian_Aid_Platform
docker compose config --quiet
docker compose build api
docker compose up -d
docker compose ps -a
docker compose logs --tail 70 api
```

The `sqlserver` service should show `Up (healthy)`, and `api` should show `Up`. `Up (healthy)` is an **output status, not a command**.

On first successful backend startup, the API calls `SeedData.InitialiseAsync` then `ExtendedModules.EnsureSchemaAsync`, creating **four new tables only if absent**: `PublicAlerts`, `Households`, `Feedback`, `PartnerOffers`. Existing tables and records are preserved. No manual SQL view installation is required for these four new features.

If the API exits, get diagnostics **without deleting the database**:

```powershell
docker compose ps -a
docker compose logs --tail 100 api
```

If you see `Login failed for user 'sa'`, the root `.env` password does not match the password already set inside SQL Server. Do not reset SQL Server to fix this; restore its actual known password.

## E. Launch the frontend

Open a separate terminal:

```powershell
cd C:\Project\Humanitarian_Aid_Platform\frontend
npm.cmd install
npm.cmd run build
npm.cmd run dev
```

Open the `Local:` address displayed by Vite (5173 or configured 5174). Leave the frontend terminal running.

Backend health: `http://localhost:8080/api/health`.
Swagger: `http://localhost:8080/swagger` (development mode).

## F. Use the new sections

1. **Public alerts:** coordinators/admins publish verified `Advisory`, `Warning`, or `Critical` notices in English, Sinhala or Tamil (typed by a human). Other authenticated users may read notices.
2. **My household:** enter locality, household size, contact and accessibility needs. Use a test account; do not enter sensitive real-life details.
3. **Assistance feedback:** after a request reaches `Fulfilled` or `Closed`, the submitting citizen can submit one rating and comment.
4. **Partner organisations:** coordinators/logistics/partner staff submit resource offers; authorised coordinators/logistics manually accept or decline. Stock is **not** automatically increased.
5. **Decision intelligence:** transparent 14-day historical demand baseline, flagged possible duplicates and verified volunteer skill suggestions. All actual decisions remain human-approved.

Previous screens remain: request submission, crisis management, AI triage/review, inventory/allocations, shelters, volunteers, donations, maps, notifications, analytics and audit. Google sign-in still requires your own OAuth client credentials.

## G. Verify data in SQL Server Management Studio

Connect using server `localhost,14333`, login `sa`, and the **existing** SQL Server password. Check **Trust server certificate** for this local development instance.

Choose **CrisisAid**, not `master`, then run:

```sql
USE CrisisAid;
GO
SELECT name FROM sys.tables WHERE name IN ('PublicAlerts','Households','Feedback','PartnerOffers');
SELECT TOP 10 * FROM dbo.PublicAlerts ORDER BY CreatedAt DESC;
```

## H. If something goes wrong

- `npm` unrecognised: reinstall Node.js LTS or use `npm.cmd` in PowerShell.
- `port 5173 in use`: set `VITE_FRONTEND_PORT=5174` in `frontend/.env.local`, plus `FRONTEND_URL=http://localhost:5174` in root `.env`; recreate the API after root change.
- `401 Unauthorized`: log in with your registered email/password; the demo password is only for demo accounts. A Google-only account must log in with Google.
- `Failed to fetch`: check `docker compose ps -a` and `http://localhost:8080/api/health`.
- Docker `api` exits: inspect `docker compose logs --tail 100 api`, not the browser alone.
- In SSMS, `Invalid object name`: select the **CrisisAid** database, not `master`.
- Do not delete Docker SQL volumes or use `docker compose down -v`.

## I. Required coursework evidence

Capture authentic screenshots of the new pages, request -> AI review -> human override -> resource/shelter allocation -> citizen notification -> audit history. Run API tests and `dotnet test` if the .NET SDK is installed. Update report, diagrams, contribution sections and presentation with **actual** evidence. The existing coursework deadline is 30 November 2026.

## Important limitations

This is a coursework prototype, not a production emergency system. Public notices are stored in-app; no real SMS or push service is automatically configured. Languages are selected but not automatically translated. Demand forecasting is a simple historical baseline. Actual fraud-detection ML, multi-stop route optimisation, payment processing and offline-first synchronisation are not implemented. Google OAuth requires Google Cloud setup. Database schema additions are idempotent but this is not a production-grade migration framework. Review security and privacy before using real personal data.
