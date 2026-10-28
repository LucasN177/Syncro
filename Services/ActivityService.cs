using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models.Database;

namespace Syncro.Services;

public class ActivityService(IActivityRepository activityRepository) : IActivityService
{
    public Task<IResponse<IReadOnlyList<ActivityDto>>> GetRecentActivity(int limit = 10, Guid? projectId = null) =>
        activityRepository.GetRecentActivity(Math.Clamp(limit, 1, 100), projectId);
}
