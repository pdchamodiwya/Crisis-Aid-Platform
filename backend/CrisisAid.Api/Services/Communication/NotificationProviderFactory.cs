namespace CrisisAid.Api.Services.Communication;

public class NotificationProviderFactory(
    IEnumerable<INotificationProvider> providers)
{
    public INotificationProvider GetProvider(
        string channel)
    {
        return providers.FirstOrDefault(
            p => p.Channel.Equals(
                channel,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Unsupported channel: {channel}");
    }
}