# CrisisAid frontend setup (Windows)

From PowerShell:

```powershell
cd C:\Project\Humanitarian_Aid_Platform\frontend
npm.cmd install
npm.cmd run dev
```

Open the exact `Local:` URL printed by Vite. The default is http://localhost:5173.
If port 5173 is busy, copy `.env.example` to `.env.local`, set `VITE_FRONTEND_PORT=5174`, and restart `npm.cmd run dev`.
**Also** set `FRONTEND_URL=http://localhost:5174` in the project root `.env`, then from the root run `docker compose up -d --force-recreate api` so Google OAuth redirects return to the right port.

The React client calls `/api`, which is proxied to the backend at http://localhost:8080. The backend and SQL Server must both be running for login to work.

The new feature pages are Public Alerts, Household, Feedback, Partner Workspace and Decision Intelligence. All require a signed-in session. Publishing public alerts is restricted to coordinator/admin roles; partner offers require authorised staff; actual aid allocations still require separate human approval.
