# CrisisAid — Module Technical Contribution Document
## Member 03: Resource, Logistics and Partner Coordination

**Role:** Senior Full-Stack Software Engineer & Module Lead
**Module:** Resource Inventory, Stock Movements, Shortage Management, Logistics Dispatch, Partner Transfers & QR Delivery Verification
**Academic Unit:** NSBM | SE205.3 Software Architecture | Coursework 2026

---

## 1. Executive Summary

This document details the architectural design, implementation, and integration of the **Resource, Logistics and Partner Coordination** submodule (Member 03) within the CrisisAid platform. The module upgrades the initial baseline resource inventory into an end-to-end humanitarian supply chain and field logistics tracking system. It incorporates intelligent multi-constraint matching, equitable priority-weighted shortage resolution, serializable reservation and allocation, live dispatch tracking, inter-agency partner NGO stock transfers, and cryptographically secure QR-based proof-of-delivery verification.

---

## 2. Gap Analysis: What Existed vs. What Was Missing

| Feature Domain | Existing Baseline Before Upgrade | Missing Gaps Identified | Implemented in Member 03 |
|---|---|---|---|
| **Inventory Tracking** | Basic `ResourceItem` (Name, Category, AvailableQuantity, Area, Accessible, Expiry). | No batch tracking, no condition status, no total vs available vs reserved ledger, no soft deactivation, no detailed descriptions. | Extended `ResourceItem` with `TotalQuantity`, `ReservedQuantity`, `Condition`, `BatchNumber`, `Description`, `IsActive`, and timestamps. Added full CRUD, low-stock (≤15) and expiring-soon queries. |
| **Inventory Movements** | Direct in-place mutation of quantity without audit trail. | Quantities were overwritten silently. No historical record of why or when stock changed. | Implemented `StockMovement` entity tracking 8 distinct movement types (`Stock Received`, `Stock Reserved`, `Stock Dispatched`, `Stock Delivered`, `Stock Returned`, `Stock Adjusted`, `Stock Damaged`, `Stock Expired`) on every inventory change. |
| **Stock Replenishment & Adjustments** | None. Only initial creation allowed. | Inability to record incoming supplies, aid donations, physical inventory count adjustments, or damaged/expired goods. | Built `POST /api/resources/{id}/replenish` and `POST /api/resources/{id}/adjust` with reason logging, batch number tracking, and movement audit creation. |
| **Resource Matching Engine** | Basic distance and category equality check in `Support.cs`. | No evaluation of resource condition, no expiry safety buffer, no rationale explanation for human coordinators, no remaining unmet demand calculation. | Enhanced `MatchEngine.ResourcesFor` into an explainable heuristic scoring algorithm that awards condition bonuses, evaluates distance penalties, calculates allocatable vs remaining unmet demand, and generates human-readable rationales. |
| **Shortage Management** | Rejected if `AvailableQuantity < RequestedQuantity`. | Inability to analyze competing cases, no prioritization by triage urgency/medical need, no suggested partial allocation plan, no shortage queue. | Implemented `ShortageResolutionService` analyzing aggregate deficit, ranking competing cases by priority weight, generating equitable partial allocation plans, and supporting one-click execution. |
| **Allocation & Reservation** | Basic `POST /api/resources/allocate` reserved available stock. | No allocation cancellation, no tracking of reserved vs remaining demand on allocation entity. | Enhanced `ResourceAllocation` with `QuantityRequested`, `QuantityAllocated`, `QuantityRemaining`, `Status`, `Notes`. Built `POST /api/allocations/{id}/cancel` to safely release reserved stock back to available inventory. |
| **Logistics & Dispatch Management** | None. Workflow ended at reservation. | No vehicle or driver tracking, no departure/destination records, no delivery status progression (`ReadyForDispatch` → `InTransit` → `Delivered`), no partial delivery reconciliation. | Implemented `DispatchRecord` entity, `DispatchService`, and endpoints for vehicle dispatch, driver assignment, real-time status progression, and automated inventory reconciliation. |
| **Partner Organisation Coordination** | Basic `PartnerOffer` entity with accept/decline. | No inter-agency requisitioning mechanism, no physical stock transfer workflow, no inventory crediting upon goods arrival. | Created `PartnerResourceRequest` and `StockTransfer` entities, supporting complete transfer pipeline (`Requested` → `Offered` → `Accepted` → `InTransit` → `Received`), with atomic crediting to physical warehouse inventory upon inspection. |
| **Delivery Verification (Advanced)** | None. Delivery relied on unverifiable manual claims. | High risk of aid diversion, ghost deliveries, or recipient impersonation in disaster zones. | Implemented `DeliveryVerificationService` generating 192-bit cryptographic tokens, 8-character human shortcodes, and pure C# vector SVG QR codes with single-use invalidation, replay prevention, and privacy-safe citizen masking. |
| **Operations Dashboard** | Simple table with 3 stat cards. | No category breakdown, no delivery pipeline visibility, no shortage monitoring, no movement audit tab, no QR scanner interface. | Upgraded `Resources.jsx` and `Partners.jsx` into tabbed operations dashboards with KPI cards, search/filter bars, stock progress visualizers, and interactive modals. |

---

## 3. Database Schema Changes & Idempotent Migration

### 3.1 Extended Existing Tables
- **`dbo.Resources`**: Added `TotalQuantity` (int), `ReservedQuantity` (int), `Description` (nvarchar), `Condition` (nvarchar), `BatchNumber` (nvarchar), `IsActive` (bit), `CreatedAt` (datetime2), `UpdatedAt` (datetime2).
- **`dbo.ResourceAllocations`**: Added `QuantityRequested` (int), `QuantityAllocated` (int), `QuantityRemaining` (int), `Notes` (nvarchar).

### 3.2 New Entities Created
1. **`dbo.StockMovements`**: `Id` (PK), `ResourceId` (FK), `MovementType`, `Quantity`, `ReferenceId`, `Notes`, `PerformedBy` (FK), `CreatedAt`.
2. **`dbo.Dispatches`**: `Id` (PK), `AllocationId` (FK), `VehicleId`, `AssignedDriver`, `AssignedVolunteerId` (FK nullable), `DepartureLocation`, `Destination`, `DepartureTime`, `EstimatedArrivalTime`, `ActualArrivalTime`, `DispatchedQuantity`, `DeliveredQuantity`, `DeliveryStatus`, `DeliveryNotes`, `CreatedAt`, `UpdatedAt`.
3. **`dbo.PartnerRequests`**: `Id` (PK), `TargetOrganisationId` (FK), `RequestedByOrganisationId` (FK), `CreatedBy` (FK), `Category`, `Quantity`, `Urgency`, `Notes`, `Status`, `CreatedAt`.
4. **`dbo.StockTransfers`**: `Id` (PK), `PartnerOfferId` (FK nullable), `FromOrganisationId` (FK), `ToOrganisationId` (FK), `Category`, `ResourceName`, `Quantity`, `Status`, `SentAt`, `ReceivedAt`, `ReceivedBy` (FK nullable), `DiscrepancyNotes`, `TargetResourceId` (FK nullable), `CreatedAt`.
5. **`dbo.DeliveryVerifications`**: `Id` (PK), `DispatchId` (FK), `VerificationToken` (Unique Index), `ShortCode` (Index), `QrCodeSvg`, `Status`, `ExpiresAt`, `VerifiedAt`, `VerifiedBy` (FK nullable), `DeliveredQuantityConfirmed`, `RecipientNotes`, `CreatedAt`.

### 3.3 Zero-Data-Loss Migration Strategy
All schema enhancements were integrated into `ExtendedModules.EnsureSchemaAsync(AidDbContext db)` using idempotent SQL DDL (`IF COL_LENGTH(...) IS NULL ALTER TABLE ...` and `IF OBJECT_ID(...) IS NULL CREATE TABLE ...`). This guarantees that existing Docker containers, test databases, or local instances upgrade smoothly without dropping data or requiring destructive database resets.

---

## 4. Software Architecture Patterns Applied

1. **Modular Monolith & Layered Separation**:
   - Clean segregation across Domain (`Entities.cs`), Data Context (`AidDbContext.cs`), Contracts (`Requests.cs`), Application Services (`LogisticsService.cs`, `ShortageResolutionService.cs`, `DispatchService.cs`, `PartnerCoordinationService.cs`, `DeliveryVerificationService.cs`), and Minimal API Endpoint Registrars (`LogisticsEndpoints.cs`).
2. **Strategy & Factory Pattern**:
   - Extended `IResourceMatchStrategy` and `ResourceMatchStrategyFactory` to support both strict full-stock matching and shortage-tolerant partial matching.
3. **Transactional Outbox & Audit Logging**:
   - All mutations trigger structured audit entries in `dbo.Audits` via `TraceLog.Add()` and append notification messages to `dbo.Outbox` and `dbo.Notifications`.
4. **Serializable Concurrency Isolation**:
   - Stock allocations, cancellations, replenishments, and delivery reconciliations execute under `System.Data.IsolationLevel.Serializable` transactions, preventing overselling, negative inventory, or race conditions during concurrent field operations.
5. **State Machine Workflow**:
   - Formal status lifecycle enforced across Dispatches:
     $$\text{ReadyForDispatch} \longrightarrow \text{Dispatched} \longrightarrow \text{InTransit} \longrightarrow \text{Delivered} \;/\; \text{PartiallyDelivered} \;/\; \text{Failed} \;/\; \text{Cancelled}$$
   - And across Inter-Agency Stock Transfers:
     $$\text{Requested} \longrightarrow \text{Offered} \longrightarrow \text{Accepted} \longrightarrow \text{InTransit} \longrightarrow \text{Received}$$

---

## 5. Algorithmic Explanations

### 5.1 Intelligent Matching Algorithm (`MatchEngine.ResourcesFor`)
Evaluates candidates matching the requested category that are active, non-expired, and non-damaged:
$$\text{Score} = S_{\text{sufficiency}} + S_{\text{accessibility}} + S_{\text{condition}} + S_{\text{expiry}} + \max(0, 30 - d_{\text{Haversine}})$$
Where:
- $S_{\text{sufficiency}} = 45$ if Available $\ge$ Requested, else $15$.
- $S_{\text{accessibility}} = 15$ if accessible when required.
- $S_{\text{condition}} = 15$ for Good/New, $5$ for Fair.
- $S_{\text{expiry}} = 10$ if non-perishable or $>30$ days buffer, $5$ if $>7$ days.
- $d_{\text{Haversine}}$ is the great-circle distance in kilometers between request and warehouse coordinates.
- Each result includes `AvailableForFullRequest`, `AllocatableQuantity`, `RemainingUnmetDemand`, and a clear plain-text `Rationale` string for coordinator review.

### 5.2 Shortage Resolution Prioritization Algorithm
When aggregate demand exceeds available units in a category, competing requests are ranked by priority weight:
$$W = W_{\text{urgency}} + W_{\text{medical}} + W_{\text{accessibility}} + \min(15, 2 \times N_{\text{affected}})$$
Where $W_{\text{urgency}}$ is $90$ for Critical, $70$ for High, $40$ for Medium, and $20$ for Low; $W_{\text{medical}} = 15$; $W_{\text{accessibility}} = 10$. Stock is allocated to the highest-weighted requests first, and residual demand is placed into an outstanding shortage queue for replenishment or inter-agency requisitioning.

### 5.3 QR-Based Delivery Verification
- When an allocation is dispatched, a 192-bit cryptographic token (`RandomNumberGenerator.Fill`) and an 8-character human shortcode (e.g. `AID-4KD2`) are generated.
- A pure C# QR matrix encoder (`QrMatrixEncoder`) constructs a standard ISO Version 3/4 byte-mode matrix with Reed-Solomon Error Correction Level M and generates a lightweight vector SVG (`<svg viewBox="0 0 ..."><path .../></svg>`).
- Recipient/driver scans QR or enters shortcode in the verification portal. Minimal consignment details are displayed with PII masked (no citizen names or private phone numbers exposed).
- On confirmation, the token is marked `Verified`, preventing token reuse/replay attacks. If delivered quantity is less than dispatched quantity, the difference is atomically restored to available stock as `Stock Returned`.

---

## 6. Testing & Quality Assurance Summary

A comprehensive test suite was authored in `backend/CrisisAid.Tests/LogisticsTests.cs` validating:
1. `Matching_ExcludesDamagedExpiredAndInaccessibleResources`: Confirms unsuitable/damaged/expired batches are omitted.
2. `Matching_CalculatesPartialAllocationAndRemainingShortage`: Validates allocatable quantity and shortage calculation.
3. `Matching_RanksCloserWarehouseHigherThanFarWarehouse`: Confirms Haversine proximity scoring.
4. `Matching_AwardsBonusScoreForGoodConditionAndAccessibleMatch`: Confirms condition bonus.
5. `DeliveryVerification_GeneratesCryptographicallySecureTokens`: Validates 48-character hex token entropy.
6. `DeliveryVerification_GeneratesValidHumanReadableShortCode`: Validates 9-char format and exclusion of ambiguous characters.
7. `DeliveryVerification_GeneratesValidSvgQrCode`: Validates vector SVG XML structure.
8. `QrMatrixEncoder_ProducesExpectedSizeAndFinderPatterns`: Validates ISO finder and timing patterns.
9. `StockMovement_MovementTypesAreCorrectlyDefined`: Validates movement auditing.
10. `ResourceItem_TrackingFieldsInitializeWithSafeDefaults`: Validates schema defaults.
11. `DispatchRecord_StatusProgression_FollowsLogisticsWorkflow`: Validates state transition lifecycle.
12. `ShortageResolution_PriorityScorePrioritizesCriticalAndMedicalCases`: Validates triage priority weighting formula.

---

## 7. Instructions for Demonstrating Module 03

1. **Sign in as Logistics Manager** (`logistics@demo.local` with configured demo password).
2. Open **Resources** (`/resources`).
3. **Inventory Management**:
   - Filter by category ("Food"), area, or toggle "Low Stock".
   - Click **New Resource** to register a new batch ("Emergency Blankets", 50 units, Condition: Good). Notice the stock movement logged under the **Stock Movements Audit** tab.
   - Click **Replenish** on an item to add +20 units. Verify that available stock and total stock increment atomically.
4. **Intelligent Matching & Shortage Resolution**:
   - Open **Match & Allocate**. Select an approved assistance request.
   - Observe ranked inventory with proximity distance, suitability points, and explainable rationale tags.
   - Open **Shortage Management**. Inspect categories with deficits. Click **Generate Allocation Plan** to review prioritized allocation across competing requests.
5. **Dispatch & Delivery**:
   - Under **Match & Allocate**, approve a reservation.
   - Click **Dispatch**, enter vehicle number (`WP-CAD-5541`) and driver name. Confirm departure.
   - Switch to **Dispatch & Delivery** tab. Observe vehicle status in transit.
   - Click **Show QR** to view the generated vector QR code and 8-character verification shortcode.
6. **Delivery Verification Portal**:
   - Switch to **QR Delivery Verification** tab. Enter the shortcode.
   - Observe privacy-protected consignment details. Enter confirmed received quantity and click **Confirm Receipt**.
   - Verify token invalidation (attempting re-entry blocks replay) and check inventory reconciliation.
7. **Partner Workspace**:
   - Open **Partners** (`/partners`).
   - Under **Resource Offers**, accept an incoming pledge.
   - Under **Requests to Partners**, issue an emergency supply requisition to an NGO.
   - Under **Inter-Agency Stock Transfers**, view active shipments and click **Confirm Receipt** to credit transfer directly into physical warehouse stock.
