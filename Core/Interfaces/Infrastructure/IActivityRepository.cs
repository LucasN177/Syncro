using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IActivityRepository
{
    /// <summary>Neueste Einträge, optional auf ein Projekt beschränkt.</summary>
    Task<IResponse<IReadOnlyList<ActivityDto>>> GetRecentActivity(int limit, Guid? projectId = null);
}
