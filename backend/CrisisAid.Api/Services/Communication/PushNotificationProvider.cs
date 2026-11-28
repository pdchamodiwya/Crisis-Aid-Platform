var subscription = new WebPush.PushSubscription(
    storedSubscription.Endpoint,
    storedSubscription.P256dh,
    storedSubscription.Auth
);

var vapidDetails = new WebPush.VapidDetails(
    config["Push:Subject"],
    config["Push:PublicKey"],
    config["Push:PrivateKey"]
);

var payload =
    System.Text.Json.JsonSerializer.Serialize(new
    {
        title = message.Subject,
        body = message.Body,
        eventKey = message.EventKey
    });

using var client = new WebPush.WebPushClient();

await client.SendNotificationAsync(
    subscription,
    payload,
    vapidDetails,
    cancellationToken);