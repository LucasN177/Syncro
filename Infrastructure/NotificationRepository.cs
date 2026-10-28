using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class NotificationRepository(Supabase.Client client) : INotificationRepository
{
    public async Task<IResponse<IReadOnlyList<NotificationDto>>> GetNotifications(int limit)
    {
        try
        {
            // RLS liefert nur eigene Benachrichtigungen
            var result = await client.From<NotificationDto>()
                .Order(notification => notification.CreatedAt, Ordering.Descending)
                .Limit(limit)
                .Get();

            return Response<IReadOnlyList<NotificationDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<NotificationDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> MarkAsRead(Guid id)
    {
        try
        {
            await client.From<NotificationDto>()
                .Where(notification => notification.Id == id)
                .Set(notification => notification.ReadAt!, DateTime.UtcNow)
                .Update();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse> MarkAllAsRead()
    {
        try
        {
            await client.From<NotificationDto>()
                .Where(notification => notification.ReadAt == null)
                .Set(notification => notification.ReadAt!, DateTime.UtcNow)
                .Update();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse> Delete(Guid id)
    {
        try
        {
            await client.From<NotificationDto>()
                .Where(notification => notification.Id == id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
