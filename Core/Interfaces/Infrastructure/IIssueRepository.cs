using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IIssueRepository
{
    Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(Guid projectId);

    /// <summary>Issues mehrerer Projekte, z. B. für Dashboard und Reports.</summary>
    Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(IEnumerable<Guid> projectIds);

    Task<IResponse<IssueDto>> GetIssue(Guid id);
    Task<IResponse<IssueDto>> CreateIssue(IssueDto issue);
    Task<IResponse<IssueDto>> UpdateIssue(IssueDto issue);

    /// <summary>Speichert nur Spalte und Position, z. B. nach Drag &amp; Drop.</summary>
    Task<IResponse> MoveIssue(Guid id, string status, double position);

    Task<IResponse> DeleteIssue(Guid id);
}
