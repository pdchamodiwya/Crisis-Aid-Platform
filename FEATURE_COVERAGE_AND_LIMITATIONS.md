# SE205.3 feature coverage — extended CrisisAid prototype

The project is a coursework prototype and NOT a live humanitarian system. The following distinction is intentional and important for the report.

| Scenario capability | Current implementation | Notes |
|---|---|---|
| Citizen email registration, JWT login, Google OAuth redirect | Implemented with configuration | Google requires real OAuth client ID/secret; account roles are restricted |
| Household management | Added: stored in SQL Server | Self-service profile |
| Emergency request submission, review and tracking | Existing core workflow | Citizen creates; staff reviews |
| Human AI review / approve / modify / defer / reject | Existing core workflow | Original recommendation and review audited |
| Duplicate detection | Existing candidate detection plus new dashboard | Does not delete valid requests |
| Resource multi-constraint matching & shortage rejection | Existing backend allocation logic | Transactional allocation; new shortage-pressure view added |
| Shelter capacity/accessibility checks | Existing backend | Human-approved allocation |
| Volunteer tasks, acceptance and completion | Existing backend | New skill candidate search added |
| Donation campaign and pledge registration | Existing backend | Actual payment processing NOT implemented (optional) |
| Public verified alerts | Added: SQL and React | In-app publication; **not** SMS/push broadcast |
| Multilingual content | Manual alert language selection | Automated translation requires external integration and is NOT included |
| Notifications / asynchronous outbox | Existing backend | In-app; email depends on SMTP settings |
| Mapping and location | Existing map + geocode + Haversine distance | Real road routing and reverse geocoding NOT included |
| Partner-organisation coordination | Added: offers and human review | Does not automatically inject stock into inventories |
| Household feedback after fulfilment | Added: SQL and React | One citizen feedback per fulfilled/closed request |
| AI decision intelligence | Added: historical demand baseline, anomaly heuristics, volunteer matches | NOT trained ML, not conclusive fraud, not optimised scheduling |
| Audit and admin | Existing backend | Role changes and logged human decisions |
| SQL persistence | SQL Server with EF Core | Four additive tables created automatically on startup |
| HTTPS | Dev HTTP localhost for ease of setup | Deployment requires secure TLS termination |
| Secure evidence upload | HTTPS evidence URL reference | Binary file storage/upload NOT implemented |
| Public safety guidance | Public notices feature | Live authority data not integrated |
| Advanced operational metrics | Counts, override rate, occupancy, shortfall, historical baseline | Not all metrics in coursework implemented (e.g. average response time) |
| Disaster demand forecasting | Simple extrapolation from last 14 days | Not robust AI prediction |
| Offline-first request capture | Not implemented | Optional bonus |

## Verification limits

Static JSX syntax checks and ZIP content integrity checks are performed. Full .NET and npm builds are **not verified** in this packaging environment; npm registry connectivity is unavailable. The student should build locally and record real unit/API/UAT results. Do not claim unexecuted tests passed. Production security, privacy, resilience and real external-service verification remain necessary before real-world use.
