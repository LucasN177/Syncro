using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class ActivityRepository(Supabase.Client client) : IActivityRepository
{
    public async Task<IResponse<IReadOnlyList<ActivityDto>>> GetRecentActivity(int limit, Guid? projectId = null)
    {
        try
        {
            var query = client.From<ActivityDto>();

            var result = projectId is { } id
                ? await query.Where(activity => activity.ProjectId == id)
                    .Order(activity => activity.CreatedAt, Ordering.Descending)
                    .Limit(limit)
                    .Get()
                : await query
                    .Order(activity => activity.CreatedAt, Ordering.Descending)
                    .Limit(limit)
                    .Get();

            return Response<IReadOnlyList<ActivityDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ActivityDto>>.Failure(exception.Message);
        }
    }
}
