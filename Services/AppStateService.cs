namespace Syncro.Services;

public class AppStateService
{
    public bool IsDarkMode { get; private set; }

    public bool IsDrawerOpen { get; set; } = true;
    
    public event Action? OnChange;

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        NotifyStateChanged();
    }
    private void NotifyStateChanged() => OnChange?.Invoke();
    
    public void DrawerToggle()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }
}