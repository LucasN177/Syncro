using Microsoft.JSInterop;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Syncro.Infrastructure;

/// <summary>
/// Speichert die Supabase-Session im localStorage, damit ein Reload nicht abmeldet.
/// In WebAssembly sind synchrone JS-Aufrufe über IJSInProcessRuntime möglich.
/// </summary>
public class LocalStorageSessionPersistence(IJSInProcessRuntime js) : IGotrueSessionPersistence<Session>
{
    private const string StorageKey = "syncro.supabase.session";

    public void SaveSession(Session session)
    {
        js.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(session));
    }

    public void DestroySession()
    {
        js.InvokeVoid("localStorage.removeItem", StorageKey);
    }

    public Session? LoadSession()
    {
        try
        {
            var json = js.Invoke<string?>("localStorage.getItem", StorageKey);
            return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<Session>(json);
        }
        catch (JsonException)
        {
            DestroySession();
            return null;
        }
    }
}
