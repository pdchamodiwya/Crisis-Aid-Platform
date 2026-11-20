# Database design

Microsoft SQL Server 2022 is the required relational backend. EF Core Code First uses `AidDbContext` and `EnsureCreated()` as a prototype bootstrap; the first API start creates all mapped tables and indexes. **Do not run an ad hoc SQL schema creation script before first start.**

The `analytics_views.sql` file optionally adds three reporting views **after** the API's first successful start. The `verification_queries.sql` file provides demonstration queries and evidence capture points for SQL Server Management Studio. No database screenshots have been fabricated.

## Main entities and relationships

- `RoleDefinitions` / `PermissionDefinitions` -> `RolePermissions`; `Users.RoleId` -> `RoleDefinitions`.
- `Users` -> `Requests` (`CitizenId`), `Volunteers` (`UserId`), `Donations` (`DonorId`), `Notifications` (`RecipientId`).
- `Crises` -> `Requests` (`CrisisId`), `Campaigns` (`CrisisId`), `ServiceAreas` (`CrisisId`).
- `Requests` -> `RequestHistories`, `AiRecommendations`, `ResourceAllocations`, `ShelterAllocations`, optionally `VolunteerTasks`.
- `Resources` -> `ResourceAllocations`, and `Shelters` -> `ShelterAllocations`.
- `Volunteers` -> `VolunteerTasks`, `Campaigns` -> `Donations`.
- `Outbox.EventKey` and `Users.Email` carry unique indices. Audit records are appended through application workflows.

Foreign-key identifiers are encoded as `Guid` fields and shown in the ER diagram. The EF Core model defines restrictive foreign-key constraints with `HasOne(...).WithMany(...).HasForeignKey(...)`. Production upgrades should use reviewed EF migrations and may add tenant isolation, soft delete and row-version concurrency fields. Critical stock and shelter capacity changes already use SQL serializable transactions.
