using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class IssueRepository(Supabase.Client client) : IIssueRepository
{
    public async Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(Guid projectId)
    {
        try
        {
            var result = await client.From<IssueDto>()
                .Where(issue => issue.ProjectId == projectId)
                .Order(issue => issue.Position, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<IssueDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<IssueDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IReadOnlyList<IssueDto>>> GetIssues(IEnumerable<Guid> projectIds)
    {
        try
        {
            var ids = projectIds.Distinct().Select(id => id.ToString()).ToList();
            if (ids.Count == 0)
                return Response<IReadOnlyList<IssueDto>>.Success([]);

            var result = await client.From<IssueDto>()
                .Filter(issue => issue.ProjectId, Operator.In, ids)
                .Order(issue => issue.Position, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<IssueDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<IssueDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IssueDto>> GetIssue(Guid id)
    {
        try
        {
            var result = await client.From<IssueDto>()
                .Where(issue => issue.Id == id)
                .Single();

            return result is not null
                ? Response<IssueDto>.Success(result)
                : Response<IssueDto>.Failure("Issue wurde nicht gefunden.");
        }
        catch (Exception exception)
        {
            return Response<IssueDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IssueDto>> CreateIssue(IssueDto issue)
    {
        try
        {
            var result = await client.From<IssueDto>().Insert(issue);
            var savedIssue = result.Models.FirstOrDefault();
            return savedIssue is not null
                ? Response<IssueDto>.Success(savedIssue)
                : Response<IssueDto>.Failure("Issue konnte nicht angelegt werden.");
        }
        catch (Exception exception)
        {
            return Response<IssueDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IssueDto>> UpdateIssue(IssueDto issue)
    {
        try
        {
            var result = await client.From<IssueDto>()
                .Where(existingIssue => existingIssue.Id == issue.Id)
                .Update(issue);

            var savedIssue = result.Models.FirstOrDefault();
            return savedIssue is not null
                ? Response<IssueDto>.Success(savedIssue)
                : Response<IssueDto>.Failure("Issue konnte nicht gespeichert werden.");
        }
        catch (Exception exception)
        {
            return Response<IssueDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> MoveIssue(Guid id, string status, double position)
    {
        try
        {
            var result = await client.From<IssueDto>()
                .Where(issue => issue.Id == id)
                .Set(issue => issue.Status, status)
                .Set(issue => issue.Position, position)
                .Update();

            return result.Models.Count > 0
                ? Response.Success()
                : Response.Failure("Issue konnte nicht verschoben werden.", null);
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse> DeleteIssue(Guid id)
    {
        try
        {
            await client.From<IssueDto>()
                .Where(issue => issue.Id == id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
