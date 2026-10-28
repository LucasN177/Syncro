using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface ICommentRepository
{
    Task<IResponse<IReadOnlyList<IssueCommentDto>>> GetComments(Guid issueId);
    Task<IResponse<IssueCommentDto>> AddComment(Guid issueId, string body);
    Task<IResponse<IssueCommentDto>> UpdateComment(Guid id, string body);
    Task<IResponse> DeleteComment(Guid id);
}
