using Blazor.Analytics;
using Squil;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration(DefaultAppConfiguration.ConfigureConfigurationBuilder);

var services = builder.Services;
var configuration = builder.Configuration;

var settings = configuration.Get<AppSettings>();

services.Configure<AppSettings>(configuration);

if (settings.UseProminentSources)
{
    services.Configure<List<ProminentSourceConfiguration>>(configuration.GetSection("Connections"));
    services.AddSingleton<ISquilConfigStore, AppSettingsSquilConfigStore>();
}
else
{
    services.AddSingleton<ISquilConfigStore, LocalFileSquilConfigStore>();
}

services.AddRazorPages();
services.AddServerSideBlazor();
services.AddSingleton<LiveConfiguration>();
services.AddSingleton<LocationQueryRunner>();

services.AddSquilDb(settings, configuration);
services.AddCommonSquilServices(settings);

var googleAnalyticsToken = configuration["GoogleAnalyticsToken"];
if (!String.IsNullOrEmpty(googleAnalyticsToken))
{
    services.AddGoogleAnalytics(googleAnalyticsToken);
}

var app = builder.Build();

app.Services.InitializeDb();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
