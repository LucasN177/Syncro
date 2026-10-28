using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IProjectRepository
{
    /// <summary>Alle Projekte, in denen der Nutzer Mitglied ist (über RLS gefiltert).</summary>
    Task<IResponse<IReadOnlyList<ProjectDto>>> GetProjects();
    Task<IResponse<ProjectDto>> GetProject(Guid id);
    Task<IResponse<ProjectDto>> CreateProject(ProjectDto project);
    Task<IResponse<ProjectDto>> UpdateProject(ProjectDto project);
    Task<IResponse> DeleteProject(Guid id);
}
