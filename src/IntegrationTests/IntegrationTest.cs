namespace ForkForward.Mapper.IntegrationTests;

public abstract class IntegrationTest<TInitializer> : AutoMapperSpecBase, IAsyncLifetime where TInitializer : IInitializer, new()
{
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
    Task IAsyncLifetime.InitializeAsync() => new TInitializer().Migrate();
}
public interface IInitializer
{
    Task Migrate();
}
public class DropCreateDatabaseAlways<TContext> : IInitializer where TContext : DbContext, new()
{
    protected virtual void Seed(TContext context){}
    public async Task Migrate()
    {
        await using var context = new TContext();
        var database = context.Database;
        await database.EnsureDeletedAsync();
        var strategy = database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () => await database.EnsureCreatedAsync());

        Seed(context);

        await context.SaveChangesAsync();
    }
}
public abstract class LocalDbContext : DbContext
{
    // SQL Server LocalDB exists only on Windows. To run these tests on another system, or against another server,
    // set FORKFORWARD_TEST_SQLSERVER to a SQL Server connection string without a database name, for example
    // "Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True".
    static readonly string Server = Environment.GetEnvironmentVariable("FORKFORWARD_TEST_SQLSERVER") is { Length: > 0 } server ?
        server.TrimEnd(';') : @"Data Source=(localdb)\mssqllocaldb;Integrated Security=True";
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer(
        $"{Server};MultipleActiveResultSets=True;Database={DatabaseName};Connection Timeout=300",
        o => o.EnableRetryOnFailure(maxRetryCount: 10).CommandTimeout(120));
    // One database per context type. Without the namespace: the longest names were already close to the
    // 128 characters SQL Server allows, and LocalDB failed on them once the namespace grew.
    string DatabaseName => GetType().FullName.Replace("ForkForward.Mapper.IntegrationTests.", "");
}