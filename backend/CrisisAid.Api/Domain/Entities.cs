using System.ComponentModel.DataAnnotations;

namespace CrisisAid.Api.Domain;

public static class Roles
{
    public const string Citizen = "Citizen";
    public const string Coordinator = "EmergencyCoordinator";
    public const string CaseManager = "CaseManager";
    public const string Logistics = "LogisticsManager";
    public const string ShelterAdmin = "ShelterAdministrator";
    public const string VolunteerCoordinator = "VolunteerCoordinator";
    public const string Volunteer = "Volunteer";
    public const string Donor = "Donor";
    public const string Funding = "FundingOfficer";
    public const string Partner = "PartnerOfficer";
    public const string Admin = "SystemAdministrator";
    public const string Staff = Coordinator + "," + CaseManager + "," + Logistics + "," + ShelterAdmin + "," + VolunteerCoordinator + "," + Funding + "," + Partner + "," + Admin;
}
public class RoleDefinition { public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get;set;}=""; public string Description {get;set;}=""; }
public class PermissionDefinition { public Guid Id {get;set;}=Guid.NewGuid(); public string Code {get;set;}=""; public string Description {get;set;}=""; }
public class RolePermission { public Guid RoleDefinitionId {get;set;} public Guid PermissionDefinitionId {get;set;} }
public class Organisation { public Guid Id {get;set;}=Guid.NewGuid(); [MaxLength(120)] public string Name {get;set;}=""; public string Country {get;set;}="Sri Lanka"; public string Type {get;set;}="NGO"; }
public class UserAccount
{
    public Guid Id {get;set;}=Guid.NewGuid(); [MaxLength(160)] public string Email {get;set;}="";
    [MaxLength(120)] public string DisplayName {get;set;}=""; public string PasswordHash {get;set;}="";
    [MaxLength(48)] public string Role {get;set;}=Roles.Citizen; public Guid? RoleId {get;set;} public Guid? OrganisationId {get;set;}
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public bool IsActive {get;set;}=true;
}
public class CrisisEvent
{
    public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get;set;}="";
    public string Type {get;set;}="Flood"; public string Country {get;set;}="Sri Lanka";
    public string Area {get;set;}=""; public string Status {get;set;}="Active";
    public DateTime StartedAt {get;set;}=DateTime.UtcNow;
}
public class ServiceArea { public Guid Id {get;set;}=Guid.NewGuid(); public Guid CrisisId {get;set;} public string Name {get;set;}=""; public double Latitude {get;set;} public double Longitude {get;set;} public double RadiusKm {get;set;}=25; }
public class AssistanceRequest
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid CitizenId {get;set;} public Guid CrisisId {get;set;}
    public string Category {get;set;}="Other"; public string Description {get;set;}="";
    public string Urgency {get;set;}="Medium"; public string Status {get;set;}="Submitted";
    public int PeopleAffected {get;set;}=1; public int RequestedQuantity {get;set;}=1;
    public bool MedicalEmergency {get;set;} public bool AccessibilityNeeded {get;set;}
    public double Latitude {get;set;} public double Longitude {get;set;}
    public string? EvidenceUrl {get;set;} public Guid? AssignedOrganisationId {get;set;}
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
}
public class RequestHistory { public Guid Id {get;set;}=Guid.NewGuid(); public Guid RequestId {get;set;} public Guid? ActorId {get;set;} public string Action {get;set;}=""; public string Notes {get;set;}=""; public DateTime At {get;set;}=DateTime.UtcNow; }
public class AiRecommendation
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid RequestId {get;set;} public string RecommendedCategory {get;set;}="Other";
    public int Priority {get;set;} public double Confidence {get;set;} public string Rationale {get;set;}="";
    public string Model {get;set;}="rule-based-fallback-v1"; public string? PossibleDuplicateId {get;set;}
    public string ReviewStatus {get;set;}="Pending"; public Guid? ReviewedBy {get;set;}
    public string? HumanReason {get;set;} public int? FinalPriority {get;set;}
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime? ReviewedAt {get;set;}
}
public class ResourceItem
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid? OrganisationId {get;set;} public string Name {get;set;}="";
    public string Category {get;set;}="Food"; public string Unit {get;set;}="packs";
    public int AvailableQuantity {get;set;} public string Area {get;set;}="";
    public bool Accessible {get;set;}=true; public DateTime? ExpiresAt {get;set;}
    public double Latitude {get;set;} public double Longitude {get;set;}
}
public class ResourceAllocation
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid ResourceId {get;set;} public Guid RequestId {get;set;}
    public int Quantity {get;set;} public string Status {get;set;}="Reserved";
    public Guid ApprovedBy {get;set;} public DateTime At {get;set;}=DateTime.UtcNow;
    public DateTime? DispatchedAt {get;set;}
}
public class Shelter
{
    public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get;set;}="";
    public string Area {get;set;}=""; public int Capacity {get;set;} public int Occupancy {get;set;}
    public bool Accessible {get;set;} public string Facilities {get;set;}="";
    public double Latitude {get;set;} public double Longitude {get;set;} public string Status {get;set;}="Open";
}
public class ShelterAllocation
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid ShelterId {get;set;} public Guid RequestId {get;set;}
    public int PeopleCount {get;set;} public Guid ApprovedBy {get;set;} public DateTime At {get;set;}=DateTime.UtcNow;
}
public class VolunteerProfile
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid UserId {get;set;} public string Skills {get;set;}="";
    public string Area {get;set;}=""; public bool Available {get;set;}=true; public bool Verified {get;set;}=false;
}
public class VolunteerTask
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid? RequestId {get;set;} public Guid VolunteerId {get;set;}
    public string RequiredSkill {get;set;}=""; public string Description {get;set;}=""; public string Status {get;set;}="Assigned";
    public Guid AssignedBy {get;set;} public DateTime At {get;set;}=DateTime.UtcNow; public DateTime? CompletedAt {get;set;}
}
public class DonationCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public Guid CrisisId { get; set; }

    public Guid? OrganisationId { get; set; }

    public decimal TargetAmount { get; set; }

    public string Currency { get; set; } = "LKR";

    public string Status { get; set; } = "Open";

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
}
public class Donation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampaignId { get; set; }

    public Guid DonorId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "LKR";

    public string Kind { get; set; } = "Monetary";

    public string Status { get; set; } = "PledgeRecorded";

    public string? TransactionReference { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    public DateTime? ReceivedAt { get; set; }

    public Guid? ConfirmedBy { get; set; }
}
public class Notification
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid RecipientId {get;set;} public string Title {get;set;}="";
    public string Message {get;set;}=""; public string Language {get;set;}="en";
    public bool Read {get;set;}=false; public DateTime At {get;set;}=DateTime.UtcNow;
    public string? EventKey { get; set; }
}
public class OutboxMessage
{
    public Guid Id {get;set;}=Guid.NewGuid(); public string EventKey {get;set;}="";
    public Guid RecipientId {get;set;} public string Subject {get;set;}=""; public string Body {get;set;}="";
    public string Status {get;set;}="Pending"; public int Attempts {get;set;}=0;
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime? NextAttemptAt {get;set;}
    public string? LastError {get;set;}
    public string Channel { get; set; } = "Email";

public string Language { get; set; } = "en";

public string? ProviderMessageId { get; set; }

public DateTime? SentAt { get; set; }

public DateTime? LockedUntil { get; set; }

public Guid? ClaimToken { get; set; }

public Guid? PushSubscriptionId { get; set; }
}
public class AuditEvent
{
    public Guid Id {get;set;}=Guid.NewGuid(); public string EntityType {get;set;}=""; public Guid EntityId {get;set;}
    public string EventType {get;set;}=""; public Guid? ActorId {get;set;}
    public string DetailJson {get;set;}="{}"; public DateTime At {get;set;}=DateTime.UtcNow;
}

// Extended humanitarian modules: data is persisted and actions are audited.
public class PublicAlert { public Guid Id {get;set;}=Guid.NewGuid(); public Guid CrisisId {get;set;} public string Headline {get;set;}=""; public string Message {get;set;}=""; public string Severity {get;set;}="Advisory"; public string Language {get;set;}="en"; public string Status {get;set;}="Published"; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public Guid PublishedBy {get;set;} }
public class HouseholdProfile { public Guid Id {get;set;}=Guid.NewGuid(); public Guid UserId {get;set;} public string Area {get;set;}=""; public int Members {get;set;}=1; public bool AccessibilityNeeded {get;set;} public string Contact {get;set;}=""; public DateTime UpdatedAt {get;set;}=DateTime.UtcNow; }
public class AssistanceFeedback { public Guid Id {get;set;}=Guid.NewGuid(); public Guid RequestId {get;set;} public Guid CitizenId {get;set;} public int Rating {get;set;} public string Comments {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
public class PartnerOffer { public Guid Id {get;set;}=Guid.NewGuid(); public Guid OrganisationId {get;set;} public Guid CreatedBy {get;set;} public string ResourceCategory {get;set;}=""; public string Area {get;set;}=""; public int Quantity {get;set;} public string Notes {get;set;}=""; public string Status {get;set;}="Offered"; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
