using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var credentialDirectory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY");
if (!string.IsNullOrWhiteSpace(credentialDirectory))
{
    var credentialPath = Path.Combine(credentialDirectory, "db-connection");
    if (File.Exists(credentialPath))
        builder.Configuration["Database:ConnectionString"] = File.ReadAllText(credentialPath).Trim();
}
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/db", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration["Database:ConnectionString"];
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("Database connection is not configured.", statusCode: 503);

    try
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1
            ? Results.Ok(new { status = "ok", database = "mysql" })
            : Results.Problem("Database probe failed.", statusCode: 503);
    }
    catch (Exception exception) when (exception is MySqlException or TimeoutException)
    {
        app.Logger.LogWarning(exception, "MySQL health probe failed");
        return Results.Problem("Database is unavailable.", statusCode: 503);
    }
});

app.Run();
