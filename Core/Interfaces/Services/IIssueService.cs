using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;

namespace Syncro.Core.Interfaces.Services;

public interface IIssueService
{
    Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(Guid projectId);
    Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(IEnumerable<Guid> projectIds);
    Task<IResponse<IssueDto>> GetIssue(Guid id);

    /// <summary>Legt das Issue an (Id leer) oder speichert es. Neue Issues landen am Ende ihrer Spalte.</summary>
    Task<IResponse<IssueDto>> SaveIssue(IssueDto issue);

    /// <summary>Verschiebt ein Issue in eine Spalte, zwischen die angegebenen Nachbarn.</summary>
    Task<IResponse<double>> MoveIssue(Guid id, Column column, double? positionBefore, double? positionAfter);

    Task<IResponse> DeleteIssue(Guid id);

    Task<IResponse<IReadOnlyList<IssueCommentDto>>> GetComments(Guid issueId);
    Task<IResponse<IssueCommentDto>> AddComment(Guid issueId, string body);
    Task<IResponse<IssueCommentDto>> UpdateComment(Guid id, string body);
    Task<IResponse> DeleteComment(Guid id);

    Task<IResponse<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid issueId);

    /// <summary>Lädt eine Datei (max. 10 MB) als Anhang an das Issue hoch.</summary>
    Task<IResponse<AttachmentDto>> AddAttachment(IssueDto issue, string fileName, string? contentType, byte[] content);

    /// <summary>Kurzlebiger Link zum Öffnen oder Herunterladen.</summary>
    Task<IResponse<string>> GetAttachmentUrl(AttachmentDto attachment);

    Task<IResponse> DeleteAttachment(AttachmentDto attachment);
}
