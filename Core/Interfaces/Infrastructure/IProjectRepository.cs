using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IProjectRepository
{
    Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects();
    Task<IResponse<ProjectDto>> CreateProject(ProjectDto project);
    Task<IResponse<ProjectDto>> UpdateProject(ProjectDto project);
}
