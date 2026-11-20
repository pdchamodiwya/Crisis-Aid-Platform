# SE205.3 Test Strategy and Evidence Matrix

**Status:** *Prepared test designs only; live test execution is required on a working .NET + SQL Server + React environment.* Enter observed result and attach genuine evidence before submission.

| ID | Layer | Scenario / action | Expected result | Evidence to capture |
|---|---|---|---|---|
| UT-01 | Unit | Critical medical request compared with low-urgency request | Higher priority for medical emergency | `dotnet test` output |
| UT-02 | Unit | High weighted factors exceed 100 | Priority clamped to 100 | `dotnet test` output |
| UT-03 | Unit | “Other” request mentions drinking water | Classified as Water | Unit test output |
| UT-04 | Unit | Accessibility-required request matches stock | Inaccessible / expired stock excluded | Unit test output |
| UT-05 | Unit | Full-quantity matching versus partial matching | Full-only filters insufficient stock | Unit test output |
| API-01 | API | POST `/api/auth/login` with correct account | 200 + JWT, no password hash | Swagger/Postman |
| API-02 | Security | Invalid login password | 401 Unauthorized | Swagger/Postman |
| API-03 | Security | Citizen requests `/api/audit` | 403 Forbidden | Swagger/Postman |
| API-04 | API | POST valid request as citizen | 201 + request ID | Swagger + SQL |
| API-05 | API | POST request with invalid latitude | 400 Bad Request | Postman |
| API-06 | AI | POST assess for submitted request | New pending recommendation, model, confidence and rationale | Swagger + SQL |
| API-07 | AI | POST human `Modified` review with reason | Original AI score retained, final score/actor/time stored | Swagger + audit DB |
| API-08 | AI | POST human `Rejected` without reason | 400 Bad Request | Postman |
| API-09 | Allocation | Reserve category-matching stock after review | 200; quantity decreases; reservation/audit/outbox saved | SQL + web UI |
| API-10 | Shortage | Request more units than stock has | 409; no stock decrement | Postman + DB before/after |
| API-11 | Shelter | Request placement exceeds remaining capacity | 409; occupancy unchanged | Swagger + DB |
| API-12 | Shelter | Accessible requirement but inaccessible shelter | 409; no allocation | Swagger |
| API-13 | Volunteers | Assign unverified volunteer | 409; no task created | Swagger |
| API-14 | Volunteers | Verified skilled volunteer task Accept → Complete | Successful state transitions; available restored | UI + SQL |
| API-15 | Notifications | Send same logical event twice via retried operation | One outbox EventKey; duplicate business operation rejected | SQL Outbox |
| API-16 | Provider failure | Configure invalid AI endpoint then assess | Heuristic fallback recorded | API + log |
| INT-01 | Integration | React citizen form to SQL Server | Valid request visible to coordinator | Screenshots/screenshare |
| INT-02 | Integration | Coordinator overrides AI, logistics allocates | Approval gate enforced across modules | Recorded demo |
| INT-03 | Integration | Map search / map tile provider unavailable | Manual coordinate entry remains available | Video or UI captures |
| INT-04 | Integration | SMTP host omitted | In-app notification delivered; outbox `InAppOnly` | DB + UI |
| UAT-01 | Acceptance | Citizen submits a Water request | Receipt and Submitted status | Video |
| UAT-02 | Acceptance | Coordinator assesses and reviews | Explanation visible; human approval stored | Video |
| UAT-03 | Acceptance | Logistics handles a shortage | Conflict reported without double allocation | Video |
| UAT-04 | Acceptance | Shelter officer assigns household | Occupancy updated, cap protected | Video |
| UAT-05 | Acceptance | Citizen views fulfilment notification | Notification appears once | Video |
| UAT-06 | Acceptance | Admin inspects decisions | Actor, recommendation, confidence, action, time visible | Video |

## Recording template (fill with actual execution)

| Test ID | Date & environment | Result (PASS/FAIL/NOT RUN) | Evidence path / link | Defect ID |
|---|---|---|---|---|
| UT-01..05 | ___ | NOT RUN | ___ | ___ |
| API-01..16 | ___ | NOT RUN | ___ | ___ |
| INT-01..04 | ___ | NOT RUN | ___ | ___ |
| UAT-01..06 | ___ | NOT RUN | ___ | ___ |

## Load, resilience and security extensions

Run at least 20 simultaneous allocations competing for 10 units of a resource; check final stock is nonnegative. Restart SQL Server during a noncritical read; verify client error display and recovery. Disable SMTP during dispatch; confirm in-app delivery and outbox retry behaviour. Attempt cross-user access to another citizen’s request. Change a JWT role client-side without a valid signature; verify 401. Capture database screenshot demonstrating the audit trail and SQL state after each test.
