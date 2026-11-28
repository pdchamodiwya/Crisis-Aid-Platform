using CrisisAid.Api.Domain;

namespace CrisisAid.Api.Services.Communication;

public record DeliveryResult(
    string Status,
    string? ProviderMessageId = null
);

public interface INotificationProvider
{
    string Channel { get; }

    Task<DeliveryResult> SendAsync(
        OutboxMessage message,
        UserAccount recipient,
        CancellationToken cancellationToken
    );
}