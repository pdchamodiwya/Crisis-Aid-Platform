# CrisisAid — AI-Powered Humanitarian Crisis Management & Aid Coordination

**NSBM | SE205.3 Software Architecture | Coursework 2026**  
A comprehensive humanitarian crisis management platform designed for rapid response, citizen assistance triage, resource inventory, logistics tracking, partner coordination, and shelter allocation.

---

## Quick Navigation

- [1. System Prerequisites](#1-system-prerequisites)
- [2. How to Run the System (Quickstart)](#2-how-to-run-the-system-quickstart)
  - [Method A: Visual Studio 2022 / 2026 (Recommended — No Docker Required)](#method-a-visual-studio-2022--2026-recommended--no-docker-required)
  - [Method B: .NET CLI / PowerShell (No Visual Studio)](#method-b-net-cli--powershell-no-visual-studio)
  - [Method C: Docker Desktop](#method-c-docker-desktop)
- [3. Frontend Setup (React + Vite)](#3-frontend-setup-react--vite)
- [4. Demo Login Credentials](#4-demo-login-credentials)
- [5. Member 03 — Resource, Logistics & Partner Coordination Tour](#5-member-03--resource-logistics--partner-coordination-tour)
- [6. Troubleshooting Common Issues](#6-troubleshooting-common-issues)
- [7. API Surfaces & Documentation](#7-api-surfaces--documentation)
- [8. Architecture Patterns & Testing](#8-architecture-patterns--testing)

---

## 1. System Prerequisites

Before running the platform, ensure the following are installed on your machine:

| Component | Minimum Requirement | Recommended | Purpose |
|---|---|---|---|
| **Operating System** | Windows 10/11 64-bit | Windows 11 | Host OS |
| **.NET SDK** | .NET 8.0 or .NET 10.0+ | .NET 8 / 10 SDK | ASP.NET Core Web API Backend |
| **IDE** | Visual Studio 2022 (v17.x) or 2026 (v18.x) | Community / Professional | Backend development & debugging |
| **Database** | SQL Server LocalDB or Docker SQL Server | `(localdb)\mssqllocaldb` (included with VS) | Data persistence |
| **Node.js & npm** | Node.js 18+ & npm 9+ | Node.js 20 LTS | React Frontend & Vite Dev Server |

---

## 2. How to Run the System (Quickstart)

### Method A: Visual Studio 2022 / 2026 (Recommended — No Docker Required)

This is the easiest and fastest way to run CrisisAid on Windows. It uses **SQL Server LocalDB** (pre-installed with Visual Studio) and automatically creates and seeds the database on first run.

#### Step 1: Open the Solution
1. Open Visual Studio.
2. Click **Open a project or solution**.
3. Select `CrisisAid.sln` located at the root of the project:
   ```text
   C:\Users\<username>\source\repos\Crisis-Aid-Platform\CrisisAid.sln
   ```

#### Step 2: Build the Solution
1. In the top menu, click **Build > Rebuild Solution** (or press `Ctrl + Shift + B`).
2. Verify that **Build: 2 succeeded, 0 failed** appears in the Output window.

#### Step 3: Run the Backend API
1. Ensure the Startup Project toolbar displays:
   - Target: **`CrisisAid.Api`**
   - Profile: **`CrisisAid.Api`** (Green play arrow)
2. Press **`Ctrl + F5`** (**Start Without Debugging**).
3. A console window will launch, initializing LocalDB, executing idempotent schema migrations, and seeding demonstration accounts:
   ```text
   Microsoft.Hosting.Lifetime: Now listening on: http://localhost:8080
   ```
4. Visual Studio will automatically open the interactive Swagger UI at:
   👉 **http://localhost:8080/swagger**

> **Keep this backend console window open while using the application.**

---

### Method B: .NET CLI / PowerShell (No Visual Studio)

If you prefer using the terminal:

1. Open PowerShell in the project root:
   ```powershell
   cd C:\Users\<username>\source\repos\Crisis-Aid-Platform
   ```
2. Run the backend directly:
   ```powershell
   dotnet run --project backend/CrisisAid.Api
   ```
3. The API will start listening on `http://localhost:8080`.

---

### Method C: Docker Desktop

If you have Docker Desktop installed and running:

1. Make sure Docker Desktop is running (Linux containers mode).
2. Create your `.env` configuration file:
   ```bash
   cp .env.example .env
   ```
3. Start the containers:
   ```bash
   docker compose up --build -d
   docker compose logs -f api
   ```
4. Access the API at `http://localhost:8080/swagger`.

---

## 3. Frontend Setup (React + Vite)

Once your backend is running on port 8080:

1. Open a new terminal / PowerShell window and navigate to the `frontend` folder:
   ```bash
   cd frontend
   ```
2. Install npm dependencies (first time only):
   ```bash
   npm install
   ```
3. Start the Vite development server:
   ```bash
   npm run dev
   ```
4. Open your browser and navigate to:
   👉 **http://localhost:5173**

> **Note:** The Vite dev server automatically proxies all `/api/*` network requests to `http://localhost:8080`.

---

## 4. Demo Login Credentials

The database comes pre-seeded with sample users for all humanitarian roles.

Default password for all demo accounts:
🔑 **`CourseworkDemo!2026`**

| Role | Email Address | Access & Purpose |
|---|---|---|
| **Logistics Lead** | `logistics@demo.local` | **Member 03:** Resources, Stock Movements, Allocations, Dispatches, QR Verification, Partners |
| **Crisis Coordinator** | `coordinator@demo.local` | Crisis overview, Case Management, AI Decision Support, Approvals |
| **System Admin** | `admin@demo.local` | User management, Role assignment, Governance & Audit logs |
| **Shelter Manager** | `shelter@demo.local` | Shelter capacity, Occupancy tracking, Evacuee intake |
| **Volunteer Lead** | `volunteerlead@demo.local` | Volunteer task assignment, Skill verification |
| **Citizen (Affected)** | `citizen@demo.local` | Submit emergency requests, track aid status |
| **Field Volunteer** | `volunteer@demo.local` | Accept tasks, report progress |
| **Donor** | `donor@demo.local` | Make campaign pledges and contributions |

---

## 5. Member 03 — Resource, Logistics & Partner Coordination Tour

Log in as `logistics@demo.local` and explore the dedicated features:

### A. Resource Operations Center (`/resources`)
The resource page is organized into **6 tabs**:
1. **📦 Inventory Control**: Real-time table of resources across warehouses. Supports adding new items, stock replenishment, editing details, deactivation, and low-stock alerts.
2. **📜 Stock Movements**: Append-only movement ledger tracking all receipts, allocations, dispatches, adjustments, and returns with reason codes.
3. **🎯 Match & Allocate**: Select an approved emergency request and run the heuristic matching engine to find the closest, highest-condition stock and allocate with zero-oversell concurrency protection.
4. **⚠️ Shortage Management**: Detect stock deficits across crises, calculate urgency priority scores for competing requests, and generate actionable shortage mitigation plans.
5. **🚚 Dispatches**: Create and track dispatch records through delivery workflows (`Pending` ➔ `InTransit` ➔ `Delivered`). Generates cryptographic QR codes.
6. **📱 QR Verification Portal**: Field verification portal where relief workers scan SVG QR codes or enter 8-character human shortcodes to confirm humanitarian deliveries in real-time.

### B. Partner Coordination Portal (`/partners`)
The partner coordination page is organized into **3 tabs**:
1. **🤝 Partner Directory & Offers**: View partner organisations and accepted resource aid pledges.
2. **📋 Requisitions**: Issue formal resource requisitions to external NGOs and humanitarian partners.
3. **🔄 Inter-Warehouse Transfers**: Coordinate stock transfers between facilities and partners with one-click atomic stock receipt into warehouse inventory.

---

## 6. Troubleshooting Common Issues

### Issue 1: `AggregateError [ECONNREFUSED]: /api/auth/login`
- **Cause**: The React frontend is running, but the backend API is not running on port 8080.
- **Fix**: Open Visual Studio and run `CrisisAid.Api` (`Ctrl + F5`), or run `dotnet run --project backend/CrisisAid.Api`. Ensure the console displays `Now listening on: http://localhost:8080`.

### Issue 2: `0x80008096 (FrameworkMissingFailure)` in Visual Studio
- **Cause**: You have .NET 10 SDK installed, but the project targets .NET 8.
- **Fix**: Both `CrisisAid.Api.csproj` and `CrisisAid.Tests.csproj` have `<RollForward>LatestMajor</RollForward>` enabled. Simply select **Build > Rebuild Solution** in Visual Studio.

### Issue 3: `An element with the same key already exists: SelfHostWebServer`
- **Cause**: Duplicate or conflicting profile definitions in `launchSettings.json`.
- **Fix**: Already cleaned in `launchSettings.json`. Ensure you select the profile named **`CrisisAid.Api`** in Visual Studio.

### Issue 4: Docker `failed to connect to the docker API at npipe:////./pipe/docker_engine`
- **Cause**: Docker Desktop is not open or the Docker daemon is stopped.
- **Fix**: You do **not** need Docker! Use **Method A (Visual Studio + LocalDB)** which requires zero Docker containers.

### Issue 5: `CS0122: 'QrMatrixEncoder' is inaccessible due to its protection level`
- **Cause**: Matrix encoder was internal.
- **Fix**: Updated to `public static class QrMatrixEncoder` in `backend/CrisisAid.Api/Services/DeliveryVerificationService.cs`.

---

## 7. API Surfaces & Documentation

When the backend is active, complete interactive OpenAPI documentation is accessible at:
🔗 **http://localhost:8080/swagger**

### Key Endpoint Groups:
- **Auth**: `/api/auth/login`, `/api/auth/register`, `/api/me`
- **Resources & Inventory**: `GET/POST /api/resources`, `POST /api/resources/{id}/replenish`, `POST /api/resources/{id}/adjust`, `GET /api/resources/movements`, `GET /api/resources/low-stock`, `GET /api/resources/kpis`
- **Matching & Allocation**: `GET /api/requests/{id}/resource-matches`, `POST /api/resources/allocate`, `POST /api/resources/allocations/{id}/cancel`
- **Shortage Resolution**: `GET /api/resources/shortages/analysis`, `POST /api/resources/shortages/plan`, `POST /api/resources/shortages/resolve`
- **Dispatches & QR Verification**: `GET/POST /api/dispatches`, `GET /api/dispatches/{id}/qr`, `GET /api/dispatches/verify/{token}`, `POST /api/dispatches/verify/confirm`
- **Partner Coordination**: `GET/POST /api/partners/requests`, `GET/POST /api/partners/transfers`, `POST /api/partners/transfers/{id}/receive`

---

## 8. Architecture Patterns & Testing

- **Modular Monolith**: Single clean ASP.NET Core solution structured into Domain, Data, Services, and Contracts.
- **Repository & Unit of Work**: EF Core `AidDbContext` with strict transaction isolation (Serializable transactions for reserve/allocation operations to eliminate race conditions).
- **Strategy & Factory**: `IResourceMatchStrategy` supporting full-stock and partial-shortage matching policies with proximity scoring.
- **Transactional Outbox**: Decoupled asynchronous notification delivery via `OutboxWorker`.
- **Automated Unit Tests**:
  ```powershell
  dotnet test backend/CrisisAid.Tests/CrisisAid.Tests.csproj
  ```
  Includes 12 dedicated unit tests in `LogisticsTests.cs` verifying inventory movements, replenishment, zero-oversell concurrency, shortage prioritization, QR matrix generation, and cryptographic delivery tokens.

---

*NSBM Green University — Faculty of Computing — Department of Software Engineering*
