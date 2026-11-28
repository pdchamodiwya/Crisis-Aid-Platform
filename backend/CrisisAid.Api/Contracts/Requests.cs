namespace CrisisAid.Api.Contracts;

public record RegisterDto(string Name, string Email, string Password, string Role = "Citizen");
public record LoginDto(string Email, string Password);
public record CrisisDto(string Name, string Type, string Country, string Area);
public record AssistanceDto(Guid CrisisId, string Category, string Description, string Urgency,
    int PeopleAffected, int RequestedQuantity, double Latitude, double Longitude,
    bool MedicalEmergency = false, bool AccessibilityNeeded = false, string? EvidenceUrl = null);
public record ReviewDto(string Decision, int? FinalPriority, string Reason, string? FinalCategory = null);
public record RequestStatusDto(string Status, string Notes);
public record ResourceDto(string Name, string Category, string Unit, int AvailableQuantity, string Area,
    double Latitude, double Longitude, bool Accessible = true, DateTime? ExpiresAt = null);
public record AllocateResourceDto(Guid RequestId, Guid ResourceId, int Quantity);
public record ShelterDto(string Name, string Area, int Capacity, bool Accessible, string Facilities,
    double Latitude, double Longitude);
public record AllocateShelterDto(Guid RequestId, Guid ShelterId, int PeopleCount);
public record VolunteerProfileDto(string Skills, string Area, bool Available);
public record VerifyVolunteerDto(bool Verified);
public record AssignVolunteerDto(Guid? RequestId, Guid VolunteerId, string RequiredSkill, string Description);
public record TaskStatusDto(string Status);
public record CampaignDto(string Name, Guid CrisisId, decimal TargetAmount);
public record DonationDto(Guid CampaignId, decimal Amount, string Currency, string Kind);
public record RoleDto(string Role);
public record NotifyDto(Guid RecipientId, string Title, string Message, string Language = "en");

// Member 03: Resource, Logistics and Partner Coordination DTOs
public record CreateResourceDto(string Name, string Category, string Unit, int AvailableQuantity, string Area,
    double Latitude, double Longitude, bool Accessible = true, DateTime? ExpiresAt = null,
    string Description = "", string Condition = "Good", string BatchNumber = "", Guid? OrganisationId = null);

public record UpdateResourceDto(string Name, string Category, string Unit, string Area,
    double Latitude, double Longitude, bool Accessible, DateTime? ExpiresAt,
    string Description, string Condition, string BatchNumber, bool IsActive);

public record ReplenishResourceDto(int Quantity, string Notes, string? BatchNumber = null, DateTime? ExpiresAt = null);

public record AdjustStockDto(int NewQuantity, string MovementType, string Reason);

public record ShortagePlanRequestDto(string Category, Guid? CrisisId = null);

public record ShortagePlanItemDto(Guid RequestId, Guid ResourceId, int QuantityAllocated);

public record ResolveShortagePlanDto(List<ShortagePlanItemDto> Allocations, string Notes);

public record EnhancedAllocateResourceDto(Guid RequestId, Guid ResourceId, int Quantity, string? Notes = null);

public record CreateDispatchDto(Guid AllocationId, string VehicleId, string AssignedDriver, Guid? AssignedVolunteerId,
    string DepartureLocation, string Destination, DateTime? EstimatedArrivalTime, int DispatchedQuantity, string? Notes = null);

public record UpdateDispatchStatusDto(string Status, string? Notes = null, int? DeliveredQuantity = null);

public record ConfirmDeliveryDto(int DeliveredQuantity, string? Notes = null, string? QrToken = null);

public record CreatePartnerResourceRequestDto(Guid TargetOrganisationId, string Category, int Quantity, string Urgency, string Notes);

public record CreateStockTransferDto(Guid ToOrganisationId, string Category, string ResourceName, int Quantity, Guid? PartnerOfferId = null, Guid? TargetResourceId = null);

public record ReceiveStockTransferDto(int ConfirmedQuantity, string? DiscrepancyNotes = null, Guid? CreditResourceId = null);

public record VerifyQrDeliveryDto(string Token, int DeliveredQuantity, string? Notes = null);
