using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connection = new NpgsqlConnectionStringBuilder
{
    Host = builder.Configuration["Database:Host"] ?? "localhost",
    Database = "requests",
    Username = "requests",
    Password = builder.Configuration["Database:Password"]
        ?? throw new InvalidOperationException("Set Database__Password before starting the application.")
};
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection.ConnectionString));
var app = builder.Build();

var db = app.Services.GetRequiredService<NpgsqlDataSource>();
await using (var schema = db.CreateCommand("""
    CREATE TABLE IF NOT EXISTS requests (
        id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        title varchar(120) NOT NULL,
        description varchar(2000) NOT NULL,
        created_at timestamptz NOT NULL DEFAULT now()
    )
    """))
{
    await schema.ExecuteNonQueryAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/requests", async (NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var command = db.CreateCommand("""
        SELECT id, title, description, created_at FROM requests ORDER BY id DESC
        """);
    await using var reader = await command.ExecuteReaderAsync(ct);
    var rows = new List<Request>();
    while (await reader.ReadAsync(ct))
        rows.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
    return rows;
});

app.MapPost("/api/requests", (RequestInput input, NpgsqlDataSource db, CancellationToken ct) =>
    Save(null, input, db, ct));
app.MapPut("/api/requests/{id:long}", (long id, RequestInput input, NpgsqlDataSource db, CancellationToken ct) =>
    Save(id, input, db, ct));

app.Run();

static async Task<IResult> Save(long? id, RequestInput input, NpgsqlDataSource db, CancellationToken ct)
{
    var title = input.Title?.Trim() ?? "";
    var description = input.Description?.Trim() ?? "";
    if (title.Length is < 1 or > 120 || description.Length > 2000)
        return Results.BadRequest(new { message = "Укажите название до 120 символов и описание до 2000 символов." });

    await using var command = db.CreateCommand(id is null
        ? "INSERT INTO requests (title, description) VALUES ($1, $2) RETURNING id"
        : "UPDATE requests SET title = $1, description = $2 WHERE id = $3 RETURNING id");
    command.Parameters.Add(new NpgsqlParameter { Value = title });
    command.Parameters.Add(new NpgsqlParameter { Value = description });
    if (id is not null)
        command.Parameters.Add(new NpgsqlParameter { Value = id.Value });
    var savedId = await command.ExecuteScalarAsync(ct);
    if (savedId is null) return Results.NotFound();
    return id is null
        ? Results.Created($"/api/requests/{savedId}", new { id = savedId })
        : Results.NoContent();
}

record RequestInput(string? Title, string? Description);
record Request(long Id, string Title, string Description, DateTime CreatedAt);
