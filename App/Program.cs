using System.Reflection;
using App.Configuration;
using App.Db;
using App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Ydb.Sdk;
using Ydb.Sdk.Auth;
using Ydb.Sdk.Yc;

var builder = WebApplication.CreateBuilder();
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly());

builder.Services.AddLogging();
builder.Services.AddCors();

var app = builder.Build();
var driver = await BuildDriver(app.Services);
var dbClient = new DbClient(driver);

var replicaInfo = BuildReplicaInfo(app.Services);
app.MapGet("/replica", () => replicaInfo);
app.MapGet("/records", async (CancellationToken cancellationToken) => await dbClient.GetAsync(cancellationToken));
app.MapPost("/records",
    async ([FromBody] CreateRecord record, CancellationToken cancellationToken) =>
    {
        if (record.Text.IsNullOrEmpty())
            return Results.BadRequest();
        await dbClient.CreateAsync(record.Text, cancellationToken);
        return Results.Ok();
    });

app.UseCors(corsPolicyBuilder => corsPolicyBuilder.AllowAnyOrigin());

try
{
    await dbClient.CreateTableAsync();
}
catch (Exception)
{
    Console.WriteLine("Database already exists");
}

await app.RunAsync();

static async Task<Driver> BuildDriver(IServiceProvider serviceProvider)
{
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var ydbConfig = BuildYdbConfig(serviceProvider);
    var credentials = BuildCreadentialsProvider(serviceProvider);
    var driverConfig = new DriverConfig(ydbConfig.Endpoint, ydbConfig.Database, credentials);
    var driver = new Driver(driverConfig, loggerFactory);
    await driver.Initialize();
    return driver;
}

static ReplicaInfo BuildReplicaInfo(IServiceProvider serviceProvider)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var version = configuration["BACKEND_VERSION"] ?? "undefined";
    return new ReplicaInfo(version, $"yc-service-{Guid.NewGuid().ToString()}");
}

static ICredentialsProvider BuildCreadentialsProvider(IServiceProvider serviceProvider)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var serviceAccountKeyFile = configuration["YDB_SERVICE_ACCOUNT_KEY_FILE_CREDENTIALS"];
    if (serviceAccountKeyFile is not null)
        return new ServiceAccountProvider(serviceAccountKeyFile);
    var anonymousCredentials = configuration["YDB_ANONYMOUS_CREDENTIALS"];
    if (anonymousCredentials == "1")
        return new AnonymousProvider();
    var metadataCredentials = configuration["YDB_METADATA_CREDENTIALS"];
    if (metadataCredentials == "1")
        return new MetadataProvider();
    var accessTokenCredentials = configuration["YDB_ACCESS_TOKEN_CREDENTIALS"];
    if (accessTokenCredentials is not null)
        return new TokenProvider(accessTokenCredentials);
    return new MetadataProvider();
}

static YdbConfig BuildYdbConfig(IServiceProvider serviceProvider)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new YdbConfig
    {
        Database = configuration["YDB_DATABASE"] ?? throw new ArgumentNullException(nameof(YdbConfig.Database)),
        Endpoint = configuration["YDB_ENDPOINT"] ?? throw new ArgumentNullException(nameof(YdbConfig.Endpoint))
    };
}