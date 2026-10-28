using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class CommentRepository(Supabase.Client client) : ICommentRepository
{
    public async Task<IResponse<IReadOnlyList<IssueCommentDto>>> GetComments(Guid issueId)
    {
        try
        {
            var result = await client.From<IssueCommentDto>()
                .Where(comment => comment.IssueId == issueId)
                .Order(comment => comment.CreatedAt, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<IssueCommentDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<IssueCommentDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IssueCommentDto>> AddComment(Guid issueId, string body)
    {
        try
        {
            var result = await client.From<IssueCommentDto>()
                .Insert(new IssueCommentDto { IssueId = issueId, Body = body.Trim() });

            var comment = result.Models.FirstOrDefault();
            return comment is not null
                ? Response<IssueCommentDto>.Success(comment)
                : Response<IssueCommentDto>.Failure("Kommentar konnte nicht gespeichert werden.");
        }
        catch (Exception exception)
        {
            return Response<IssueCommentDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IssueCommentDto>> UpdateComment(Guid id, string body)
    {
        try
        {
            var result = await client.From<IssueCommentDto>()
                .Where(comment => comment.Id == id)
                .Set(comment => comment.Body, body.Trim())
                .Update();

            var comment = result.Models.FirstOrDefault();
            return comment is not null
                ? Response<IssueCommentDto>.Success(comment)
                : Response<IssueCommentDto>.Failure("Nur eigene Kommentare können bearbeitet werden.");
        }
        catch (Exception exception)
        {
            return Response<IssueCommentDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> DeleteComment(Guid id)
    {
        try
        {
            await client.From<IssueCommentDto>()
                .Where(comment => comment.Id == id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
