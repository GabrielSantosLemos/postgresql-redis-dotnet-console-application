// Config PostgreSQL
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;

var pgConnString = "Host=localhost;Port=5432;Username=admin;Password=admin123;Database=empresa";

// Config Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var dbRedis = redis.GetDatabase();

int userId = 1;
string cacheKey = $"usuario:{userId}";

// Primeiro: tentar pegar do Redis
string? cacheValue = await dbRedis.StringGetAsync(cacheKey);

if (!string.IsNullOrEmpty(cacheValue))
{
    Console.WriteLine("Dados vindos do Redis (cache): " + cacheValue);
}
else
{
    Console.WriteLine("Dados não estavam no cache. Consultando PostgreSQL...");

    await using var pgConn = new NpgsqlConnection(pgConnString);
    await pgConn.OpenAsync();

    string sql = "SELECT id, nome FROM usuarios WHERE id = @id";
    await using var cmd = new NpgsqlCommand(sql, pgConn);
    cmd.Parameters.AddWithValue("id", userId);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        var usuario = new
        {
            Id = reader.GetInt32(0),
            Nome = reader.GetString(1)
        };

        string json = JsonSerializer.Serialize(usuario);
        Console.WriteLine("Resultado do PostgreSQL: " + json);

        // Salvar no Redis com expiração de 60s
        await dbRedis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(60));
    }
    else
    {
        Console.WriteLine("Usuário não encontrado no PostgreSQL.");
    }
}