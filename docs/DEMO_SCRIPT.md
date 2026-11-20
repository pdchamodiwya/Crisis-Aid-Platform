# 10-Minute Zoom / Microsoft Teams Presentation Script

This is a **speaking and screen-share template**, not a completed recording. Each real presenter must introduce name and index number; cameras must be on. Demonstrate actual working software, not these diagrams alone.

## 00:00–04:00 — Group system demonstration (4 minutes)

**0:00–0:55 — Problem and solution.** "During floods and displacement, requests, stock and shelter information can be scattered across organisations. CrisisAid provides a single operational environment. Residents submit needs; coordinators review AI suggestions; logistics and shelter officers approve allocations; every critical decision is audited." Show the dashboard and active crisis.

**0:55–1:55 — Architecture and OOP.** Show the architecture diagram. "React communicates with an ASP.NET Core modular monolith via REST. EF Core persists data in SQL Server. Service interfaces support dependency injection and a repository boundary. A matching strategy factory selects strict or partial-stock recommendations. An outbox worker sends notifications asynchronously." Highlight JWT, RBAC, transactions and timeout fallback.

**1:55–2:55 — Citizen and AI.** Log in as a citizen; submit a clear emergency request and show its status. Log in as a coordinator; open the case and select Generate AI assessment. Show priority, confidence, rationale and potential duplicates. Demonstrate **Modified** override with a reason, or **Approved**, and inspect the history record.

**2:55–4:00 — Logistics and accountability.** Log in as logistics manager; display matching inventory. Show successful allocation, then a shortage conflict. Show shelter occupancy, volunteer assignments, notifications and a saved audit event. If time is limited, use prepared safe requests and shorten page transitions.

## 04:00–10:00 — Individual contributions (6 × 1 minute)

**Member 1 [NAME] [INDEX] (4:00–5:00):** Show *actual* owned React pages, component structure, responsive behavior, validation, one implementation challenge and mitigation. Replace with real responsibility.

**Member 2 [NAME] [INDEX] (5:00–6:00):** Show *actual* backend endpoints, DTOs, authentication/RBAC work, specific test evidence and technical risks. Replace with real responsibility.

**Member 3 [NAME] [INDEX] (6:00–7:00):** Show *actual* SQL Server tables, EF Core mappings, transactions and database evidence. Explain one data-integrity challenge. Replace with real responsibility.

**Member 4 [NAME] [INDEX] (7:00–8:00):** Show *actual* AI scoring integration, fallback, human review and audit trail. Explain model uncertainty and privacy measures. Replace with real responsibility.

**Member 5 [NAME] [INDEX] (8:00–9:00):** Show *actual* logistics, shelter or volunteer workflow implemented by this person and a shortage/capacity test. Replace with real responsibility.

**Member 6 [NAME] [INDEX] (9:00–10:00):** Show *actual* external-service integrations, Postman/testing, diagrams/report work and lessons learned. Replace with real responsibility.

## Recording and hand-in

All members participate in the recorded Zoom/Teams meeting and keep cameras on, per guidelines. Upload to NSBM OneDrive with **all evaluator access** and paste the real video URL into the designated Word submission document. Also complete repository/source OneDrive links in Chapter 10 of the final report.
