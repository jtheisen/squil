using MonkeyBusters.Reconciliation.Internal;

namespace Squil;

public class LiveSourceDebugOptions
{
    public Boolean DebugFailOnModelCreation { get; set; }
    public Boolean DebugSqlFailOnThirdQuery { get; set; }
    public Boolean DebugExceptionFailOnThirdQuery { get; set; }
}

public class SqlServerHostConfiguration
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(30)]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "The name can only contain lower case ASCII characters, numbers and dashes")]
    public String Name { get; set; } = "new";

    [Required]
    [MaxLength(60)]
    public String Host { get; set; } = ".\\";

    public Boolean UseWindowsAuthentication { get; set; } = true;

    [MaxLength(60)]
    public String User { get; set; }

    [MaxLength(60)]
    public String Password { get; set; }

    [MaxLength(60)]
    public String Catalog { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public LiveSourceDebugOptions DebugOptions => null;

    public SqlServerHostConfiguration Clone() => (SqlServerHostConfiguration)MemberwiseClone();
}

public class Db : DbContext
{
    public DbSet<SqlServerHostConfiguration> SqlServerHostConfigurations { get; set; }

    public Db(DbContextOptions<Db> options)
        : base(options)
    {
    }
}

public static class DbExtensions
{
    public static async Task<T> DoAsync<T>(this IDbContextFactory<Db> factory, Func<Db, Task<T>> func)
    {
        var db = await factory.CreateDbContextAsync();

        return await func(db);
    }

    public static async Task<E> ReconcileAsync<E>(this IDbContextFactory<Db> factory, E templateEntity, Action<ExtentBuilder<E>> extent)
        where E : class
    {
        var db = await factory.CreateDbContextAsync();

        return await db.ReconcileAndSaveChangesAsync(templateEntity, extent);
    }

    public static async Task RemoveAsync<E>(this IDbContextFactory<Db> factory, E templateEntity)
        where E : class
    {
        var db = await factory.CreateDbContextAsync();

        db.Remove(templateEntity);

        await db.SaveChangesAsync();
    }
}
