# CrisisAid — AI-Powered Humanitarian Crisis Management & Aid Coordination

**NSBM | SE205.3 Software Architecture | Coursework 2026**  
A group coursework prototype based on the three supplied brief, report, and presentation guidelines. **Not a live emergency response system.** All seed records and organisations are fictional.

## 1. Contents

| Path | Content |
|---|---|
| `backend/CrisisAid.Api/` | C# ASP.NET Core 8 REST API, EF Core SQL Server, authentication, core services, AI decision-support, outbox |
| `backend/CrisisAid.Tests/` | xUnit tests for priority, category classification, matching policies and distance |
| `frontend/` | Responsive React/Vite web application (citizen portal, case dashboard, logistics, shelters, volunteers, donations, map, audit) |
| `database/` | SQL reporting views, verification queries and entity design notes |
| `diagrams/` | Architecture, use case, class, ER, component, deployment and sequence diagrams (editable sources + exported PNG/SVG) |
| `postman/` | Importable Postman collection and example environment |
| `docs/` | Final report, demonstration/presentation slides, scripts, test matrix and evidence checklist |
| `.env.example` | Secret/configuration template; local-only demo seed options |
| `docker-compose.yml` | SQL Server + API containers |

## 2. Requirements

- Docker Desktop with Linux container support (SQL Server image may require several GB RAM)
- Node.js 20+ and npm for the React frontend
- Optional .NET 8 SDK for local `dotnet test` / direct API launch
- Internet connection for first-time Docker image pulls, npm install, optional AI, mapping tiles and email
- Visual Studio Code / Visual Studio / SQL Server Management Studio as desired

## 3. Start in a local development environment

```bash
# from the project root
cp .env.example .env
```

Edit `.env` and change `SA_PASSWORD`, `JWT_KEY` (32+ bytes), and `DEMO_PASSWORD` (12+ characters). Do not reuse sample secrets outside private local demos.

```bash
docker compose up --build -d
docker compose logs -f api
```

Once API startup and SQL connection succeed, open **http://localhost:8080/swagger** to examine the APIs. SQL Server is exposed on `localhost,14333` for optional local inspection. To use HTTPS in a shared environment, terminate TLS at a secure reverse proxy; the sample local Docker endpoint is HTTP-only and should not be exposed to the internet.

In a separate terminal:

```bash
cd frontend
npm install
npm run dev
```

Open **http://localhost:5173** in your browser. The Vite development proxy forwards `/api` requests to port 8080. (Alternatively, change `VITE_API_BASE` to a deployed API.)

**Development seed accounts** (when `Demo__Enabled=true`): `coordinator@demo.local`, `admin@demo.local`, `logistics@demo.local`, `shelter@demo.local`, `volunteerlead@demo.local`, `citizen@demo.local`, `volunteer@demo.local`, and `donor@demo.local`. All use the **DEMO_PASSWORD** value you configured in `.env`. Do not publish login details in a public recording. Seed data is only generated when the Users table is empty; changing the demo password later does not reset existing accounts.

## 4. Demonstration workflow

1. Sign in as `citizen@demo.local` and open **Request assistance**. Submit a Water or Food request in the demo crisis; enter your location or click the map.
2. Sign in as `coordinator@demo.local` and open **Case management**. Select the new case; click **Generate AI assessment**. Review the category, priority, confidence and rationale. Approve, reject, defer or modify the score; include a reason for modification/rejection.
3. Sign in as `logistics@demo.local`; in **Resources**, select a verified/approved request, inspect ranked matches, and reserve a suitable quantity. Attempt a quantity larger than stock to show `409 Conflict` without overspending inventory.
4. For a Shelter request, sign in as `shelter@demo.local` and allocate within capacity and accessibility constraints.
5. Sign in as `volunteerlead@demo.local`; verify a volunteer, then assign a task requiring an available skill. Sign in as `volunteer@demo.local` to accept and complete it.
6. Sign in as `donor@demo.local` to record a **pledge**. No payment gateway is implemented and no actual funds are taken.
7. Sign in as `admin@demo.local` to inspect role assignments and the **Governance & audit** event history; check **Notifications** for in-app delivery.
8. In Swagger, test protected APIs with a bearer token; capture SQL Server rows via `database/verification_queries.sql` for the final report.

## 5. API surfaces

| Category | Representative paths |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/me` |
| Crises | `GET /api/crises`, `POST /api/crises` |
| Assistance | `GET/POST /api/requests`, `GET /api/requests/{id}`, `POST /api/requests/{id}/assess`, `POST /api/requests/{id}/review`, `PATCH /api/requests/{id}/status` |
| Allocation | `GET/POST /api/resources`, `GET /api/requests/{id}/resource-matches`, `POST /api/resources/allocate` |
| Shelters | `GET/POST /api/shelters`, `POST /api/shelters/allocate` |
| Volunteers | `GET /api/volunteers`, `PUT /api/volunteers/me`, `PATCH /api/volunteers/{id}/verify`, `GET/POST /api/volunteers/tasks` |
| Donations | `GET/POST /api/campaigns`, `GET/POST /api/donations` |
| Communications | `GET /api/notifications`, `PATCH /api/notifications/{id}/read` |
| Map | `GET /api/map/geocode?query=...`, `GET /api/map/distance?...` |
| Governance | `GET /api/dashboard`, `GET /api/audit`, `GET /api/users`, `GET /api/roles`, `PATCH /api/users/{id}/role` |

Swagger shows current request/response shapes. The Postman collection contains a repeatable authenticated workflow with environment variables.

## 6. Run checks and tests

```bash
# Requires .NET 8 SDK and initial NuGet package restore
 dotnet test backend/CrisisAid.Tests/CrisisAid.Tests.csproj
# Frontend install then production bundle
 cd frontend && npm run build
# Container startup / health
 curl http://localhost:8080/api/health
```

The enclosed test matrix provides unit, API, integration, and UAT scenarios. **Test cases and expected outcomes are supplied; full execution evidence must be captured on a machine with Docker, .NET, npm and SQL Server available.** This package was created in an environment without `dotnet` or npm registry access, so a successful build or passing live test suite is not claimed.

## 7. AI model configuration and safeguards

Default mode is an explainable **heuristic decision-support fallback**, not a trained ML model. It considers reported urgency, medical concerns, people affected, accessibility needs and category-related keywords, and returns a priority range 1–100 with an explicitly non-calibrated confidence score. If `AI_API_KEY`, `AI_MODEL` and `AI_ENDPOINT` are supplied, a chat-completions-compatible external AI can be called using only trimmed request features. The external AI call has a 6-second timeout; invalid responses and downtime fall back to the heuristic. **External AI is disabled by default.** Before activating it, obtain consent where required, redact personal information in descriptions, assess vendor retention and data residency, and validate local protection regulations.

The system never automatically approves a case or dispatches assistance from an AI result. Authorised case managers explicitly approve or override suggestions. Original priority, confidence, human action, actor, time and reason are preserved in `AiRecommendations` and the append-only-intent audit event stream. Possible duplicates are flagged; they are never automatically deleted or merged.

## 8. Reliability and security

- ASP.NET Identity password hashing; JWT expiry; server-side RBAC and ownership checks; auth/geocode rate limiting.
- **SQL serializable transactions** for reserve operations to prevent resource shortages and shelter over-capacity during concurrent requests.
- A transactional **outbox with unique event keys** to avoid duplicate notifications and a background worker for retrying SMTP delivery; every recipient also receives in-app messages.
- External AI and map geocoding use timeouts/fallbacks; external map tiles may not load offline.
- Structured audit of major mutations; event metadata retained for reconstruction of decisions.
- No raw secret is shipped in source code. `.env` is gitignored. Only development demo credentials exist through opt-in seeding.

### Known limitations (must be disclosed in report)

This is a teaching prototype, not humanitarian-production software. It lacks disaster-tested offline sync, formal data-protection impact assessment, verified language translation, actual payment collection, secure evidence-file storage (HTTPS evidence links only), cryptographically tamper-evident audit storage, comprehensive tenant-scoped access restrictions, professional incident verification, and real-time distributed job locking. The outbox is designed for a **single API instance** in the included Compose deployment. Public OpenStreetMap/Nominatim services have policies and availability limits. Unit tests are supplied but not executed in this environment. A production release would require security review, load/penetration tests, migration-based schema management, comprehensive monitoring, backup/restore exercises and formal operational signoff.

## 9. Submission checklist and ownership

The course specifies **30 November 2026** as the deadline. Replace placeholders (`GROUP_NUMBER`, student names and index numbers) in reports/slides. Record a genuine 10-minute group presentation (4-minute system demo + 1-minute contribution per member for a 6-person group) using Zoom or Teams with cameras and introductions. Upload the video and source to **NSBM OneDrive with evaluator access**, add real OneDrive/GitHub URLs to the report and hand-in Word document, and produce the institution-required NLearn submissions/ZIP. The included files do **not** invent real recording, repository links, Git commit history, student contributions or screenshots. See `docs/SUBMISSION_CHECKLIST.md`.

## 10. Software architecture patterns

- **Modular monolith:** single deployable API with isolated domain, data, services, contracts.
- **Repository:** `IRequestRepository` and `EfRequestRepository` for case-query encapsulation.
- **Dependency Injection:** ASP.NET Core container injects repositories, services and HTTP clients.
- **Strategy & Factory:** `IResourceMatchStrategy` alternatives and `ResourceMatchStrategyFactory` choose between full-stock and partial-shortage matching policies.
- **Transactional outbox / pub-sub-like workflow:** business transactions enqueue events; `OutboxWorker` processes them asynchronously into in-app or email notifications.

For diagram sources and explanations, see `diagrams/` and the final report.
