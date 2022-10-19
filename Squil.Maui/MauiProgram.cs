using Microsoft.Maui.Controls;

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
        services.AddSingleton<LiveConfiguration>();
        services.AddSingleton<LocationQueryRunner>();

        var squilFolder = GetAndEnsureSquilFolder();

        void SetAppSettings(AppSettings s)
        {
            s.SquilDbProviderName = "Sqlite";
            s.SquilDbSqliteConnectionString = $"Filename={Path.Combine(squilFolder, "squil-config.db")}";
            s.ShowNavigationChrome = true;
        };

        var settings = new AppSettings();
        SetAppSettings(settings);

        services.AddSquilDb(settings, null);
        services.AddCommonSquilServices(settings);
        services.Configure<AppSettings>(SetAppSettings);

        var app = builder.Build();

        app.Services.InitializeDb();

        return app;
    }

    static String GetSquilFolder()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "squil");

    static String GetAndEnsureSquilFolder()
    {
        var folder = GetSquilFolder();

        Directory.CreateDirectory(folder);

        return folder;
    }
}