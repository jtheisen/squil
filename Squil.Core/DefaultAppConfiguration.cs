using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Reflection;

public static class DefaultAppConfiguration
{
    //public static void WithConfiguredNLog(Action action)
    //{
    //    var log = LogManager.GetLogger("loginit");

    //    try
    //    {
    //        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    //        var configSpecifier = String.Equals(env, "production", StringComparison.InvariantCultureIgnoreCase) ? ".production" : "";

    //        var configFile = $"nlog{configSpecifier}.config";

    //        NLogBuilder.ConfigureNLog(configFile);

    //        log.Info($"NLog up with {configFile}");

    //        action();
    //    }
    //    finally
    //    {
    //        // Why do we never see this? Flushing doesn't work!
    //        log.Info("NLog shutting down");

    //        LogManager.Shutdown();
    //    }
    //}

    public static IConfiguration GetConfiguration()
    {
        var builder = new ConfigurationBuilder();
        ConfigureConfigurationBuilder(builder);
        return builder.Build();
    }

    public static void ConfigureConfigurationBuilder(IConfigurationBuilder c)
        => c
        .AddJsonFile($"{GetHomePath()}/settings/global.settings.json", optional: true)
        .AddJsonFile($"{GetHomePath()}/settings/Squil.settings.json", optional: true)
        .AddJsonFile($"{GetHomePath()}/settings/{GetConfigFileBaseNameFromCallingAssembly()}.settings.json", optional: true);

    private static string GetHomePath() => Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

    private static string GetConfigFileBaseNameFromCallingAssembly() => Assembly.GetEntryAssembly().FullName.Split(',')[0].ToLower();
}
