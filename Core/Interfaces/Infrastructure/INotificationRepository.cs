using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface INotificationRepository
{
    /// <summary>Neueste Benachrichtigungen des angemeldeten Nutzers.</summary>
    Task<IResponse<IReadOnlyList<NotificationDto>>> GetNotifications(int limit);

    Task<IResponse> MarkAsRead(Guid id);
    Task<IResponse> MarkAllAsRead();
    Task<IResponse> Delete(Guid id);
}
