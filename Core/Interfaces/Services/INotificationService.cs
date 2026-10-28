using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Services;

public interface INotificationService
{
    Task<IResponse<IReadOnlyList<NotificationDto>>> GetNotifications(int limit = 30);
    Task<IResponse> MarkAsRead(Guid id);
    Task<IResponse> MarkAllAsRead();
    Task<IResponse> Delete(Guid id);
}
