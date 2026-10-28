using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models.Database;

namespace Syncro.Services;

public class NotificationService(INotificationRepository notificationRepository) : INotificationService
{
    public Task<IResponse<IReadOnlyList<NotificationDto>>> GetNotifications(int limit = 30) =>
        notificationRepository.GetNotifications(Math.Clamp(limit, 1, 100));

    public Task<IResponse> MarkAsRead(Guid id) => notificationRepository.MarkAsRead(id);

    public Task<IResponse> MarkAllAsRead() => notificationRepository.MarkAllAsRead();

    public Task<IResponse> Delete(Guid id) => notificationRepository.Delete(id);
}
