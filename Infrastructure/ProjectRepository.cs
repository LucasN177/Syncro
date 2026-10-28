using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class ProjectRepository(Supabase.Client client) : IProjectRepository
{
    public async Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects()
    {
        try
        {
            if (client.Auth.CurrentUser is null)
                return Response<IReadOnlyList<ProjectDto>>.Failure("Bitte melde dich an, um Projekte zu laden.");

            // RLS liefert nur Projekte, in denen der Nutzer Mitglied ist
            var result = await client.From<ProjectDto>()
                .Order(project => project.CreatedAt, Ordering.Descending)
                .Get();

            return Response<IReadOnlyList<ProjectDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ProjectDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectDto>> GetProject(Guid id)
    {
        try
        {
            var result = await client.From<ProjectDto>()
                .Where(project => project.Id == id)
                .Single();

            return result is not null
                ? Response<ProjectDto>.Success(result)
                : Response<ProjectDto>.Failure("Projekt wurde nicht gefunden.");
        }
        catch (Exception exception)
        {
            return Response<ProjectDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectDto>> CreateProject(ProjectDto project)
    {
        try
        {
            var userId = client.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(userId))
                return Response<ProjectDto>.Failure("Bitte melde dich an, um ein Projekt anzulegen.");

            project.UserId = userId;
            project.CreatedAt = DateTime.UtcNow;

            var result = await client.From<ProjectDto>().Insert(project);
            var savedProject = result.Models.FirstOrDefault() ?? project;
            return Response<ProjectDto>.Success(savedProject);
        }
        catch (Exception exception)
        {
            return Response<ProjectDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectDto>> UpdateProject(ProjectDto project)
    {
        try
        {
            if (client.Auth.CurrentUser is null)
                return Response<ProjectDto>.Failure("Bitte melde dich an, um ein Projekt zu speichern.");

            // user_id (Eigentümer) wird von der Datenbank geschützt und hier nicht überschrieben
            var result = await client.From<ProjectDto>()
                .Where(existingProject => existingProject.Id == project.Id)
                .Update(project);

            var savedProject = result.Models.FirstOrDefault();
            return savedProject is not null
                ? Response<ProjectDto>.Success(savedProject)
                : Response<ProjectDto>.Failure("Nur Projekt-Admins können das Projekt bearbeiten.");
        }
        catch (Exception exception)
        {
            return Response<ProjectDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> DeleteProject(Guid id)
    {
        try
        {
            await client.From<ProjectDto>()
                .Where(project => project.Id == id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
