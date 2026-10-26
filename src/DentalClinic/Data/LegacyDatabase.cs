using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DentalClinic.Data;

public static class LegacyDatabase
{
    /// <summary>
    /// Creates or upgrades the database through the migrations.
    /// Earlier versions built the database with EnsureCreated, which leaves no migration history: such a database
    /// already has the first version of the tables, so the first migration is marked as applied instead of run
    /// (it would fail with "table already exists"), and only the newer migrations are applied.
    /// </summary>
    public static async Task MigrateAsync(this DbContext db, CancellationToken ct = default)
    {
        await AdoptIfBuiltWithEnsureCreatedAsync(db, ct);
        await db.Database.MigrateAsync(ct);
    }

    private static async Task AdoptIfBuiltWithEnsureCreatedAsync(DbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            if (await TableExistsAsync(connection, "__EFMigrationsHistory", ct)) return;
            if (!await AnyApplicationTableAsync(connection, ct)) return;   // empty database: a normal migrate will do

            var first = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).First();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY, "ProductVersion" TEXT NOT NULL);
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ($id, '10.0.0');
                """;
            var id = command.CreateParameter();
            id.ParameterName = "$id";
            id.Value = first;
            command.Parameters.Add(id);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string table, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var name = command.CreateParameter();
        name.ParameterName = "$name";
        name.Value = table;
        command.Parameters.Add(name);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<bool> AnyApplicationTableAsync(System.Data.Common.DbConnection connection, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }
}
