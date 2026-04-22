using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Supabase;
using Syncro;
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

var options = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = true
};

builder.Services.AddSingleton<Client>(provider =>
    new Client(
        url!, key, options
    )
);
builder.Services.AddSingleton<IAuthRepository, AuthRepository>();

builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

await builder.Build().RunAsync();
