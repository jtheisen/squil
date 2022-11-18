namespace Squil;

public class AppSettings
{
    public String Version { get; set; }

    public String GoogleAnalyticsToken { get; set; }

    public Boolean EnableDevMode { get; set; }

    public Boolean ShowHelpTexts { get; set; } = true;

    public Int32 InitialLimit { get; set; } = 10;

    public Int32 LoadMoreLimit { get; set; } = 100;

    public Int32? DebugQueryDelayMillis { get; set; }

    public Boolean ShowNavigationChrome { get; set; }

    // Something is off about how SQL Server reports the sizes, they seem to large.
    public Int32 PreferScanningUnderKb { get; set; } = 100000;

    public Boolean ShowDemoText { get; set; }

    public Boolean UseProminentSources { get; set; } = false;

    public String SquilDbProviderName { get; set; } = "Sqlite";
    public String SquilDbSqlServerConnectionString { get; set; }
    public String SquilDbSqliteConnectionString { get; set; } = "Filename=squil.db";
}
