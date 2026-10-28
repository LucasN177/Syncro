using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Supabase;
using Syncro;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Services;
using Syncro.Infrastructure;
using Syncro.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var url = builder.Configuration["Supabase:Url"];
var key = builder.Configuration["Supabase:AnonKey"];

builder.Services.AddSingleton<Client>(provider =>
    new Client(
        url!, key, new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true,
            // Syncro-Tabellen liegen im eigenen Schema, siehe Database/000_preflight.sql
            Schema = SyncroDatabase.Schema,
            SessionHandler = new LocalStorageSessionPersistence((IJSInProcessRuntime)provider.GetRequiredService<IJSRuntime>())
        }
    )
);
builder.Services.AddSingleton<IAuthRepository, AuthRepository>();
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddSingleton<IIssueRepository, IssueRepository>();
builder.Services.AddSingleton<ICommentRepository, CommentRepository>();
builder.Services.AddSingleton<IEventRepository, EventRepository>();
builder.Services.AddSingleton<IActivityRepository, ActivityRepository>();
builder.Services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddSingleton<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<IRealtimeRepository, RealtimeRepository>();

builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<IProjectMemberService, ProjectMemberService>();
builder.Services.AddSingleton<IIssueService, IssueService>();
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<IActivityService, ActivityService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<IRealtimeService, RealtimeService>();

var culture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var host = builder.Build();

try
{
    // Stellt eine gespeicherte Session wieder her und erneuert ggf. das Token
    await host.Services.GetRequiredService<Client>().InitializeAsync();
}
catch (Exception exception)
{
    // Kein Console.Error: das würde in Blazor WASM die Fehlerleiste einblenden
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Syncro.Startup")
        .LogWarning(exception, "Supabase konnte nicht initialisiert werden");
}

await host.Services.GetRequiredService<IAuthService>().InitializeAsync();

await host.RunAsync();
