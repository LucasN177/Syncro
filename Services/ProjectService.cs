using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Services;

public class ProjectService(IProjectRepository projectRepository) : IProjectService
{
    public Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects() => projectRepository.GetProjects();

    public Task<IResponse<ProjectDto>> CreateProject(ProjectDto project) => projectRepository.CreateProject(project);

    public Task<IResponse<ProjectDto>> SaveProject(ProjectDto project) =>
        project.Id == Guid.Empty
            ? CreateProject(project)
            : projectRepository.UpdateProject(project);
}
