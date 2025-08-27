using Microsoft.AspNetCore.SignalR;

namespace SportsData.Api.Infrastructure.Notifications;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR connection established. ConnectionId: {ConnectionId}, UserIdentifier: {UserIdentifier}, RemoteIpAddress: {RemoteIpAddress}", 
            Context.ConnectionId, 
            Context.UserIdentifier,
            Context.GetHttpContext()?.Connection.RemoteIpAddress);

        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            var groupName = $"User_{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Added connection {ConnectionId} to group {GroupName}", Context.ConnectionId, groupName);
        }
        else
        {
            _logger.LogWarning("Connection {ConnectionId} has no UserIdentifier", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "SignalR connection {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("SignalR connection {ConnectionId} disconnected normally", Context.ConnectionId);
        }

        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            var groupName = $"User_{userId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Removed connection {ConnectionId} from group {GroupName}", Context.ConnectionId, groupName);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Add a method to test connectivity
    public async Task Ping()
    {
        _logger.LogDebug("Ping received from connection {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}