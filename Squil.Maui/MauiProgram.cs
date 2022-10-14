namespace Squil.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var services = builder.Services;

        services.AddMauiBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        //services.AddSingleton<ISquilConfigStore, AppSettingsSquilConfigStore>();
        services.AddSingleton<ISquilConfigStore, LocalFileSquilConfigStore>();
        services.AddSingleton<LiveConfiguration>();
        services.AddSingleton<LocationQueryRunner>();

        services.AddCommonSquilServices(new AppSettings());

        var app = builder.Build();

        app.Services.InitializeDb();

        return app;
    }
}