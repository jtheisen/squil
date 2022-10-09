using Blazor.Analytics;
using Squil;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration(DefaultAppConfiguration.ConfigureConfigurationBuilder);

var services = builder.Services;
var configuration = builder.Configuration;

services.Configure<AppSettings>(configuration);
services.Configure<List<ConnectionConfiguration>>(configuration.GetSection("Connections"));

services.AddRazorPages();
services.AddServerSideBlazor();
services.AddSingleton<ISquilConfigStore, AppSettingsSquilConfigStore>();
//services.AddSingleton<ISquilConfigStore, LocalFileSquilConfigStore>();
services.AddSingleton<LiveConfiguration>();
services.AddSingleton<LocationQueryRunner>();

services.AddCommonSquilServices();

var googleAnalyticsToken = configuration["GoogleAnalyticsToken"];
if (!String.IsNullOrEmpty(googleAnalyticsToken))
{
    services.AddGoogleAnalytics(googleAnalyticsToken);
}

var app = builder.Build();

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
