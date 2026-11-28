namespace CrisisAid.Api.Contracts;

public record RegisterDto(string Name, string Email, string Password, string Role = "Citizen");
public record LoginDto(string Email, string Password);
public record CrisisDto(string Name, string Type, string Country, string Area);
public record AssistanceDto(Guid CrisisId, string Category, string Description, string Urgency,
    int PeopleAffected, int RequestedQuantity, double Latitude, double Longitude,
    bool MedicalEmergency = false, bool AccessibilityNeeded = false, string? EvidenceUrl = null, string Unit = "unit-unspecified");
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
