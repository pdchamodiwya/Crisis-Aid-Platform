namespace CrisisAid.Api.Domain;

public class CommunicationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Language { get; set; } = "en";

    public string? PhoneNumber { get; set; }

    public bool PhoneVerified { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool SmsEnabled { get; set; }

    public bool PushEnabled { get; set; }
}

public class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}