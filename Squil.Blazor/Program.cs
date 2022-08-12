using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Squil;
using Squil.Blazor.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration(DefaultAppConfiguration.ConfigureConfigurationBuilder);

var services = builder.Services;
var configuration = builder.Configuration;

services.Configure<AppSettings>(configuration);
services.Configure<List<ConnectionConfiguration>>(configuration.GetSection("Connections"));

services.AddRazorPages();
services.AddServerSideBlazor();
services.AddSingleton<ConnectionManager>();
services.AddSingleton<LocationQueryRunner>();

builder.Services.AddSingleton<WeatherForecastService>();

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
