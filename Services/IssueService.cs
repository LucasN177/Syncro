using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;

namespace Syncro.Services;

public class IssueService(
    IIssueRepository issueRepository,
    ICommentRepository commentRepository,
    IAttachmentRepository attachmentRepository) : IIssueService
{
    private const double PositionStep = 1024;

    /// <summary>Entspricht file_size_limit des Buckets in 008_attachments.sql.</summary>
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;

    public Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(Guid projectId) => issueRepository.GetIssues(projectId);

    public Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(IEnumerable<Guid> projectIds) => issueRepository.GetIssues(projectIds);

    public Task<IResponse<IssueDto>> GetIssue(Guid id) => issueRepository.GetIssue(id);

    public async Task<IResponse<IssueDto>> SaveIssue(IssueDto issue)
    {
        if (string.IsNullOrWhiteSpace(issue.Title))
            return Response<IssueDto>.Failure("Bitte gib einen Titel ein.");

        if (!IssueOptions.Types.Contains(issue.Type) || !IssueOptions.Priorities.Contains(issue.Priority))
            return Response<IssueDto>.Failure("Ungültiger Typ oder ungültige Priorität.");

        if (issue.Progress is < 0 or > 100)
            return Response<IssueDto>.Failure("Der Fortschritt muss zwischen 0 und 100 liegen.");

        issue.Title = issue.Title.Trim();
        issue.Status = issue.Status.ToColumn().ToStatus();
        issue.Tags = issue.Tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (issue.Id != Guid.Empty)
            return await issueRepository.UpdateIssue(issue);

        var existing = await issueRepository.GetIssues(issue.ProjectId);
        var lastPosition = existing.Data?
            .Where(other => other.Status == issue.Status)
            .Select(other => other.Position)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        issue.Position = lastPosition + PositionStep;

        return await issueRepository.CreateIssue(issue);
    }

    public async Task<IResponse<double>> MoveIssue(Guid id, Column column, double? positionBefore, double? positionAfter)
    {
        var position = (positionBefore, positionAfter) switch
        {
            (null, null) => PositionStep,
            ({ } before, null) => before + PositionStep,
            (null, { } after) => after - PositionStep,
            ({ } before, { } after) => (before + after) / 2
        };

        var result = await issueRepository.MoveIssue(id, column.ToStatus(), position);
        return result.IsSuccess
            ? Response<double>.Success(position)
            : Response<double>.Failure(result.ErrorMessage ?? "Issue konnte nicht verschoben werden.");
    }

    public Task<IResponse> DeleteIssue(Guid id) => issueRepository.DeleteIssue(id);

    public Task<IResponse<IReadOnlyList<IssueCommentDto>>> GetComments(Guid issueId) => commentRepository.GetComments(issueId);

    public async Task<IResponse<IssueCommentDto>> AddComment(Guid issueId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Response<IssueCommentDto>.Failure("Der Kommentar ist leer.");

        return await commentRepository.AddComment(issueId, body);
    }

    public async Task<IResponse<IssueCommentDto>> UpdateComment(Guid id, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Response<IssueCommentDto>.Failure("Der Kommentar ist leer.");

        return await commentRepository.UpdateComment(id, body);
    }

    public Task<IResponse> DeleteComment(Guid id) => commentRepository.DeleteComment(id);

    public Task<IResponse<IReadOnlyList<AttachmentDto>>> GetAttachments(Guid issueId) =>
        attachmentRepository.GetAttachments(issueId);

    public async Task<IResponse<AttachmentDto>> AddAttachment(IssueDto issue, string fileName, string? contentType, byte[] content)
    {
        if (issue.Id == Guid.Empty)
            return Response<AttachmentDto>.Failure("Bitte speichere das Issue, bevor du Dateien anhängst.");

        if (content.LongLength == 0)
            return Response<AttachmentDto>.Failure("Die Datei ist leer.");

        if (content.LongLength > MaxAttachmentBytes)
            return Response<AttachmentDto>.Failure("Dateien dürfen höchstens 10 MB groß sein.");

        return await attachmentRepository.Upload(issue.ProjectId, issue.Id, fileName, contentType, content);
    }

    public Task<IResponse<string>> GetAttachmentUrl(AttachmentDto attachment) =>
        attachmentRepository.CreateDownloadUrl(attachment.StoragePath, 60);

    public Task<IResponse> DeleteAttachment(AttachmentDto attachment) => attachmentRepository.Delete(attachment);
}
