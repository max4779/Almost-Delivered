using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
var passwordHasher = new PasswordHasher<string>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/db", async (IConfiguration config, CancellationToken ct) =>
{
    try
    {
        await using var db = await OpenDatabase(config, ct);
        await using var command = new MySqlCommand("SELECT 1", db);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1
            ? Results.Ok(new { status = "ok", database = "mysql" })
            : Results.Problem("Database probe failed.", statusCode: 503);
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogWarning(e, "MySQL health probe failed");
        return Results.Problem("Database is unavailable.", statusCode: 503);
    }
});

app.MapPost("/v1/profiles/guest", async (ProfileProgress progress, IConfiguration config, CancellationToken ct) =>
{
    if (!Valid(progress)) return Results.BadRequest(new { error = "invalid_progress" });
    var accountId = "guest:" + Guid.NewGuid().ToString("N");
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    try
    {
        await using var db = await OpenDatabase(config, ct);
        await using var command = new MySqlCommand(
            "INSERT INTO player_profiles (account_id, token_hash, progress_json) VALUES (@account, UNHEX(@hash), @progress)", db);
        command.Parameters.AddWithValue("@account", accountId);
        command.Parameters.AddWithValue("@hash", Hash(token));
        command.Parameters.AddWithValue("@progress", JsonSerializer.Serialize(progress));
        await command.ExecuteNonQueryAsync(ct);
        return Results.Ok(new ProfileReply(accountId, token, progress));
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogError(e, "Guest profile creation failed");
        return Results.Problem("Profile storage is unavailable.", statusCode: 503);
    }
});

app.MapPost("/v1/auth/register", async (RegisterRequest input, IConfiguration config, CancellationToken ct) =>
{
    var email = NormalizeEmail(input.Email);
    if (email == null || input.Password is not { Length: >= 10 and <= 128 })
        return Results.BadRequest(new { error = "invalid_credentials_format" });
    var accountId = "account:" + Guid.NewGuid().ToString("N");
    var token = NewToken();
    var passwordHash = passwordHasher.HashPassword(accountId, input.Password);
    try
    {
        await using var db = await OpenDatabase(config, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var progress = new ProfileProgress(1, Array.Empty<ProfileRank>());
        if (ValidToken(input.GuestToken))
        {
            await using var guest = new MySqlCommand(
                "SELECT progress_json FROM player_profiles WHERE account_id LIKE 'guest:%' AND token_hash = UNHEX(@hash)",
                db, transaction);
            guest.Parameters.AddWithValue("@hash", Hash(input.GuestToken!));
            var guestJson = await guest.ExecuteScalarAsync(ct) as string;
            if (guestJson != null)
                progress = JsonSerializer.Deserialize<ProfileProgress>(guestJson) ?? progress;
        }
        await using (var account = new MySqlCommand(
            "INSERT INTO accounts (account_id, email_normalized, password_hash) VALUES (@id, @email, @password)",
            db, transaction))
        {
            account.Parameters.AddWithValue("@id", accountId);
            account.Parameters.AddWithValue("@email", email);
            account.Parameters.AddWithValue("@password", passwordHash);
            await account.ExecuteNonQueryAsync(ct);
        }
        await using (var profile = new MySqlCommand(
            "INSERT INTO player_profiles (account_id, token_hash, progress_json) VALUES (@id, UNHEX(@hash), @progress)",
            db, transaction))
        {
            profile.Parameters.AddWithValue("@id", accountId);
            profile.Parameters.AddWithValue("@hash", Hash(NewToken()));
            profile.Parameters.AddWithValue("@progress", JsonSerializer.Serialize(progress));
            await profile.ExecuteNonQueryAsync(ct);
        }
        await InsertSession(db, transaction, accountId, token, ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new ProfileReply(accountId, token, progress));
    }
    catch (MySqlException e) when (e.Number == 1062)
    {
        return Results.Conflict(new { error = "email_in_use" });
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogError(e, "Account registration failed");
        return Results.Problem("Account storage is unavailable.", statusCode: 503);
    }
});

app.MapPost("/v1/auth/login", async (LoginRequest input, IConfiguration config, CancellationToken ct) =>
{
    var email = NormalizeEmail(input.Email);
    if (email == null || input.Password == null) return Results.Unauthorized();
    try
    {
        await using var db = await OpenDatabase(config, ct);
        string? accountId = null;
        string? passwordHash = null;
        await using (var command = new MySqlCommand(
            "SELECT account_id, password_hash FROM accounts WHERE email_normalized = @email", db))
        {
            command.Parameters.AddWithValue("@email", email);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                accountId = reader.GetString(0);
                passwordHash = reader.GetString(1);
            }
        }
        if (accountId == null || passwordHash == null ||
            passwordHasher.VerifyHashedPassword(accountId, passwordHash, input.Password) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        var token = NewToken();
        await using var transaction = await db.BeginTransactionAsync(ct);
        await InsertSession(db, transaction, accountId, token, ct);
        ProfileProgress? progress = null;
        await using (var command = new MySqlCommand(
            "SELECT progress_json FROM player_profiles WHERE account_id = @id", db, transaction))
        {
            command.Parameters.AddWithValue("@id", accountId);
            var json = await command.ExecuteScalarAsync(ct) as string;
            if (json != null) progress = JsonSerializer.Deserialize<ProfileProgress>(json);
        }
        if (progress == null) return Results.Problem("Account profile is unavailable.", statusCode: 500);
        await transaction.CommitAsync(ct);
        return Results.Ok(new ProfileReply(accountId, token, progress));
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogError(e, "Account login failed");
        return Results.Problem("Account storage is unavailable.", statusCode: 503);
    }
});

app.MapPost("/v1/auth/logout", async (HttpRequest request, IConfiguration config, CancellationToken ct) =>
{
    var token = Bearer(request);
    if (token == null) return Results.Unauthorized();
    await using var db = await OpenDatabase(config, ct);
    await using var command = new MySqlCommand(
        "DELETE FROM account_sessions WHERE token_hash = UNHEX(@hash)", db);
    command.Parameters.AddWithValue("@hash", Hash(token));
    return await command.ExecuteNonQueryAsync(ct) == 1 ? Results.NoContent() : Results.Unauthorized();
});

app.MapGet("/v1/profile", async (HttpRequest request, IConfiguration config, CancellationToken ct) =>
{
    var token = Bearer(request);
    if (token == null) return Results.Unauthorized();
    try
    {
        await using var db = await OpenDatabase(config, ct);
        await using var command = new MySqlCommand(
            "SELECT p.account_id, p.progress_json FROM player_profiles p WHERE p.token_hash = UNHEX(@hash) " +
            "UNION ALL SELECT p.account_id, p.progress_json FROM account_sessions s " +
            "JOIN player_profiles p ON p.account_id = s.account_id " +
            "WHERE s.token_hash = UNHEX(@hash) AND s.expires_at > UTC_TIMESTAMP() LIMIT 1", db);
        command.Parameters.AddWithValue("@hash", Hash(token));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Results.Unauthorized();
        var progress = JsonSerializer.Deserialize<ProfileProgress>(reader.GetString(1));
        return progress == null ? Results.Problem("Stored profile is invalid.", statusCode: 500)
            : Results.Ok(new ProfileReply(reader.GetString(0), null, progress));
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogError(e, "Profile load failed");
        return Results.Problem("Profile storage is unavailable.", statusCode: 503);
    }
});

app.MapPut("/v1/profile", async (HttpRequest request, ProfileProgress progress,
    IConfiguration config, CancellationToken ct) =>
{
    var token = Bearer(request);
    if (token == null) return Results.Unauthorized();
    if (!Valid(progress)) return Results.BadRequest(new { error = "invalid_progress" });
    try
    {
        await using var db = await OpenDatabase(config, ct);
        await using var command = new MySqlCommand(
            "UPDATE player_profiles SET progress_json = @progress WHERE account_id IN (" +
            "SELECT account_id FROM (SELECT account_id FROM player_profiles WHERE token_hash = UNHEX(@hash) " +
            "UNION ALL SELECT account_id FROM account_sessions WHERE token_hash = UNHEX(@hash) " +
            "AND expires_at > UTC_TIMESTAMP()) AS valid_account)", db);
        command.Parameters.AddWithValue("@progress", JsonSerializer.Serialize(progress));
        command.Parameters.AddWithValue("@hash", Hash(token));
        return await command.ExecuteNonQueryAsync(ct) == 1 ? Results.NoContent() : Results.Unauthorized();
    }
    catch (Exception e) when (e is MySqlException or InvalidOperationException or TimeoutException)
    {
        app.Logger.LogError(e, "Profile save failed");
        return Results.Problem("Profile storage is unavailable.", statusCode: 503);
    }
});

app.Run();

static async Task<MySqlConnection> OpenDatabase(IConfiguration config, CancellationToken ct)
{
    var connectionString = config["Database:ConnectionString"];
    if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Database is not configured.");
    var db = new MySqlConnection(connectionString);
    await db.OpenAsync(ct);
    return db;
}

static async Task InsertSession(MySqlConnection db, MySqlTransaction transaction,
    string accountId, string token, CancellationToken ct)
{
    await using var command = new MySqlCommand(
        "INSERT INTO account_sessions (token_hash, account_id, expires_at) " +
        "VALUES (UNHEX(@hash), @id, UTC_TIMESTAMP() + INTERVAL 30 DAY)", db, transaction);
    command.Parameters.AddWithValue("@hash", Hash(token));
    command.Parameters.AddWithValue("@id", accountId);
    await command.ExecuteNonQueryAsync(ct);
}

static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

static bool ValidToken(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);

static string? NormalizeEmail(string? email)
{
    if (string.IsNullOrWhiteSpace(email)) return null;
    var normalized = email.Trim().ToLowerInvariant();
    var at = normalized.IndexOf('@');
    return normalized.Length <= 254 && at is > 0 && at < normalized.Length - 1 &&
        normalized.LastIndexOf('@') == at && normalized.IndexOf('.', at + 1) >= 0 &&
        !normalized.Any(char.IsWhiteSpace) ? normalized : null;
}

static string? Bearer(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
    var token = header[7..].Trim();
    return token.Length == 64 && token.All(Uri.IsHexDigit) ? token : null;
}

static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));

static bool Valid(ProfileProgress progress)
{
    if (progress.Level is < 1 or > 20 || progress.Ranks == null || progress.Ranks.Length > 6) return false;
    var known = new HashSet<string> { "combat.cargo_fire", "combat.counter", "survival.armor",
        "survival.mobility", "escort.lead", "escort.repair" };
    var ranks = new Dictionary<string, int>();
    foreach (var entry in progress.Ranks)
        if (entry == null || !known.Contains(entry.Id) || entry.Rank is < 1 or > 5 ||
            !ranks.TryAdd(entry.Id, entry.Rank)) return false;
    if (ranks.Values.Sum() > progress.Level || ranks.Values.Sum() > 20) return false;
    foreach (var category in new[] { "combat.", "survival.", "escort." })
        if (ranks.Where(item => item.Key.StartsWith(category, StringComparison.Ordinal)).Sum(item => item.Value) > 10)
            return false;
    return (!ranks.ContainsKey("combat.counter") || ranks.GetValueOrDefault("combat.cargo_fire") >= 3) &&
        (!ranks.ContainsKey("survival.mobility") || ranks.GetValueOrDefault("survival.armor") >= 3) &&
        (!ranks.ContainsKey("escort.repair") || ranks.GetValueOrDefault("escort.lead") >= 3);
}

record ProfileProgress(int Level, ProfileRank[] Ranks);
record ProfileRank(string Id, int Rank);
record ProfileReply(string AccountId, string? Token, ProfileProgress Progress);
record RegisterRequest(string? Email, string? Password, string? GuestToken);
record LoginRequest(string? Email, string? Password);
