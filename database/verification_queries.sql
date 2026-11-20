-- Run against the 'CrisisAid' database in SQL Server Management Studio or sqlcmd.
SELECT TOP 20 Id, Category, Urgency, Status, CreatedAt FROM dbo.Requests ORDER BY CreatedAt DESC;
SELECT TOP 20 RequestId, Priority, Confidence, ReviewStatus, ReviewedBy, ReviewedAt FROM dbo.AiRecommendations ORDER BY CreatedAt DESC;
SELECT TOP 20 EntityType, EventType, ActorId, At FROM dbo.Audits ORDER BY At DESC;
SELECT TOP 20 EventKey, Status, Attempts, LastError FROM dbo.Outbox ORDER BY CreatedAt DESC;
SELECT Name, Occupancy, Capacity, Capacity-Occupancy AS SpacesOpen FROM dbo.Shelters;
SELECT Name, Category, AvailableQuantity, Unit FROM dbo.Resources;
SELECT * FROM dbo.vw_RequestStatusMetrics;
