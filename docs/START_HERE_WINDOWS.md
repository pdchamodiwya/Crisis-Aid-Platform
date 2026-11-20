# Start CrisisAid on Windows (Docker Desktop + VS Code)

1. Install **Docker Desktop** (Linux containers), **Node.js 20+**, and optionally the **.NET 8 SDK** and **SQL Server Management Studio**. Enable WSL2 for Docker if needed.
2. Extract `CrisisAid_Complete_Coursework_Package.zip`; open the `Humanitarian_Aid_Platform` folder in Visual Studio Code or PowerShell.
3. Run `Copy-Item .env.example .env`, then edit `.env` with your **own** strong `SA_PASSWORD`, `JWT_KEY` (at least 32 characters) and `DEMO_PASSWORD` (12+ characters).
4. In one terminal run `docker compose up --build -d` and then `docker compose logs -f api`. Wait for database creation and API startup.
5. In a **second** terminal, run `cd frontend`, `npm install`, then `npm run dev`. Open `http://localhost:5173`.
6. Sign in with a configured demo user such as `coordinator@demo.local`, using the `DEMO_PASSWORD` from `.env`. The API has Swagger at `http://localhost:8080/swagger`.
7. Run `dotnet test backend/CrisisAid.Tests/CrisisAid.Tests.csproj` in the root if the SDK is installed. Record the real result in `docs/TEST_PLAN.md`.
8. Populate real group numbers/names/index numbers in the DOCX/PPTX files, capture genuine UI/Swagger/SQL screenshots and Git commits, then upload source and video to OneDrive with evaluator permissions.

**If something fails:** If SQL Server does not start, verify Docker Desktop is running, Linux containers are enabled and the SA password meets the SQL Server complexity policy. If the frontend reports a connection error, check `docker compose ps` and `http://localhost:8080/api/health`. If ports are occupied, stop the conflicting application or adjust Compose/Vite ports consistently. Clear the demo database only if you intentionally want to reset sample data (this deletes stored records): `docker compose down -v`.

**Security notice:** This is a local educational demo. Do not expose HTTP port 8080 or seeded accounts to the public internet, and do not put personal emergency data or real medical requests into the demonstration system.
