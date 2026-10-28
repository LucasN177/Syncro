using Microsoft.JSInterop;

namespace Syncro.Services;

public class AppStateService
{
    private const string DarkModeKey = "syncro.darkMode";
    private const string CurrentProjectKey = "syncro.currentProject";

    private readonly IJSInProcessRuntime _js;

    public AppStateService(IJSRuntime js)
    {
        _js = (IJSInProcessRuntime)js;
        IsDarkMode = Load(DarkModeKey) == "true";
        CurrentProjectId = Guid.TryParse(Load(CurrentProjectKey), out var projectId) ? projectId : null;
    }

    public bool IsDarkMode { get; private set; }

    public bool IsDrawerOpen { get; set; } = true;

    /// <summary>Zuletzt gewähltes Projekt für Board, Kalender und Team.</summary>
    public Guid? CurrentProjectId { get; private set; }

    public event Action? OnChange;

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        Save(DarkModeKey, IsDarkMode ? "true" : "false");
        NotifyStateChanged();
    }

    public void SetCurrentProject(Guid? projectId)
    {
        if (CurrentProjectId == projectId)
            return;

        CurrentProjectId = projectId;
        Save(CurrentProjectKey, projectId?.ToString());
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void DrawerToggle()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    private string? Load(string key)
    {
        try
        {
            return _js.Invoke<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private void Save(string key, string? value)
    {
        try
        {
            if (value is null)
                _js.InvokeVoid("localStorage.removeItem", key);
            else
                _js.InvokeVoid("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // localStorage nicht verfügbar (z. B. privater Modus) – Zustand gilt dann nur für die Sitzung
        }
    }
}
