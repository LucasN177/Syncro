using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Infrastructure;

public class ProjectRepository(Supabase.Client client) : IProjectRepository
{
    public async Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects()
    {
        try
        {
            var userId = client.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(userId))
                return Response<IReadOnlyList<ProjectDto>>.Failure("Bitte melde dich an, um Projekte zu laden.");

            var result = await client.From<ProjectDto>()
                .Where(project => project.UserId == userId)
                .Order(project => project.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            return Response<IReadOnlyList<ProjectDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ProjectDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectDto>> CreateProject(ProjectDto project)
    {
        try
        {
            var userId = client.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(userId))
                return Response<ProjectDto>.Failure("Bitte melde dich an, um ein Projekt anzulegen.");

            project.Id = Guid.NewGuid();
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
            var userId = client.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(userId))
                return Response<ProjectDto>.Failure("Bitte melde dich an, um ein Projekt zu speichern.");

            project.UserId = userId;
            var result = await client.From<ProjectDto>()
                .Where(existingProject => existingProject.Id == project.Id)
                .Update(project);

            var savedProject = result.Models.FirstOrDefault() ?? project;
            return Response<ProjectDto>.Success(savedProject);
        }
        catch (Exception exception)
        {
            return Response<ProjectDto>.Failure(exception.Message);
        }
    }
}
