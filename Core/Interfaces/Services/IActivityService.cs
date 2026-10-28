using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Services;

public interface IActivityService
{
    Task<IResponse<IReadOnlyList<ActivityDto>>> GetRecentActivity(int limit = 10, Guid? projectId = null);
}
