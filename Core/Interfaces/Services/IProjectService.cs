using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Services;

public interface IProjectService
{
    Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects();
    Task<IResponse<ProjectDto>> CreateProject(ProjectDto project);
    Task<IResponse<ProjectDto>> SaveProject(ProjectDto project);
}
