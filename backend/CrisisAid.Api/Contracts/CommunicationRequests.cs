namespace CrisisAid.Api.Contracts;

public record CommunicationPreferenceInput(
    string Language,
    string? PhoneNumber,
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushEnabled
);

public record SendNotificationInput(
    Guid RecipientId,
    string Title,
    string Message,
    string Language,
    Guid EventId
);

public record PushSubscriptionInput(
    string Endpoint,
    string P256dh,
    string Auth
);