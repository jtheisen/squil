namespace Squil;

public class AppSettings
{
    public String SquilVersion { get; set; }

    public String GoogleAnalyticsToken { get; set; }

    public Boolean EnableDevMode { get; set; }

    public Boolean ShowHelpTexts { get; set; } = true;

    public Int32 InitialLimit { get; set; } = 10;

    public Int32 LoadMoreLimit { get; set; } = 100;

    public Boolean ShowNavigationChrome { get; set; }

    // Something is off about how SQL Server reports the sizes, they seem too large.
    public Int32 PreferScanningUnderKb { get; set; } = 100000;

    public Boolean EnablePrimaryIdSqlCopy { get; set; }

    public Boolean ShowDemoText { get; set; }

    public Boolean UseProminentSources { get; set; } = false;

    public String SquilDbProviderName { get; set; }
    public String SquilDbSqlServerConnectionString { get; set; }
    public String SquilDbSqliteConnectionString { get; set; }


    public Int32? DebugQueryDelayMillis { get; set; }
}
