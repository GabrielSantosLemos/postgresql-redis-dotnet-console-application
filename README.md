# Integração PostgreSQL e Redis com .NET Console

Este projeto é um exemplo prático de como **realizar integrações com
bancos de dados PostgreSQL e sistemas de cache Redis** utilizando uma
aplicação **.NET Console**.

O objetivo é demonstrar um fluxo simples de acesso a dados onde: 1. A
aplicação tenta obter informações de um usuário a partir do **Redis
(cache em memória)**. 2. Caso não encontre, consulta o **PostgreSQL
(banco de dados relacional)**. 3. O resultado obtido do PostgreSQL é
serializado em JSON e armazenado no Redis com um tempo de expiração. 4.
Em chamadas subsequentes, os dados podem ser recuperados diretamente do
Redis, reduzindo a carga no banco e melhorando a performance da
aplicação.

---

## Tecnologias utilizadas

- **.NET 8 Console Application**
- **PostgreSQL** (armazenamento persistente de dados)
- **Redis** (sistema de cache em memória)
- **Npgsql** (driver para PostgreSQL no .NET)
- **StackExchange.Redis** (cliente oficial Redis para .NET)
- **Docker / Docker Compose** para orquestração dos serviços

---

## Etapas do projeto

### 1. Criar projeto console .NET

```powershell
dotnet new console -n PostgresqlRedisDotnetConsoleApplication
cd PostgresqlRedisDotnetConsoleApplication
```

### 2. Adicionar pacotes necessários

```powershell
dotnet add package Npgsql
dotnet add package StackExchange.Redis
```

### 3. Configurar PostgreSQL e Redis no Docker

Crie um arquivo `docker-compose.yml` na raiz do projeto:

```yaml
version: "3.9"
services:
  postgres:
    image: postgres:15
    container_name: meu-postgres
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: admin123
      POSTGRES_DB: empresa
    ports:
      - "5432:5432"

  redis:
    image: redis:latest
    container_name: meu-redis
    ports:
      - "6379:6379"
```

Subir os serviços:

```powershell
docker-compose up -d
```

### 4. Criar tabela no PostgreSQL

Conectar ao banco:

```powershell
docker exec -it meu-postgres psql -U admin -d empresa
```

Criar tabela e inserir registro:

```sql
CREATE TABLE usuarios (
  id SERIAL PRIMARY KEY,
  nome VARCHAR(100)
);

INSERT INTO usuarios (nome) VALUES ('Gabriel');
```

### 5. Implementar código no .NET

Edite `Program.cs`:

```csharp
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
```

### 6. Executar a aplicação

```powershell
dotnet run
```

- Na **primeira execução**, o programa consulta o PostgreSQL e
  armazena no Redis.
- Nas **execuções seguintes** (até 60 segundos), os dados são
  retornados diretamente do Redis.

---

## Objetivo educacional

Este projeto serve como **base de aprendizado** para desenvolvedores que
desejam compreender: - Como combinar bancos de dados relacionais com
sistemas de cache em aplicações reais. - Boas práticas de performance
utilizando Redis. - Estrutura de um fluxo de **cache aside pattern** no
.NET.
