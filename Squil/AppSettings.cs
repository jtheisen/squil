using System;

namespace Squil
{
    public class AppSettings
    {
        public String GoogleAnalyticsToken { get; set; }

        public Boolean EnableDevMode { get; set; }

        public Int32 InitialLimit { get; set; } = 10;

        public Int32 LoadMoreLimit { get; set; } = 100;
    }
}
