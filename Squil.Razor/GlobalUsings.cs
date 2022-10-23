global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Threading.Tasks;
global using NLog;
global using static TaskLedgering.LedgerControl;
global using static RazorHelpers;
global using StringPairs = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.String, System.String>>;
global using IDbFactory = Microsoft.EntityFrameworkCore.IDbContextFactory<Squil.Db>;
global using Microsoft.EntityFrameworkCore;

