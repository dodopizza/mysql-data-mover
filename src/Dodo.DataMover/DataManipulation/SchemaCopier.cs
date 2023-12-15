using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dodo.DataMover.DataManipulation.DatabaseMapping;
using Dodo.DataMover.DataManipulation.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Dodo.DataMover.DataManipulation
{
    public class SchemaCopier
    {
        private readonly DataMoverSettings _dataMoverSettings;
        private readonly PolicyFactory _policyFactory;
        private readonly ILogger<SchemaCopier> _logger;

        public SchemaCopier(
            DataMoverSettings dataMoverSettings,
            PolicyFactory policyFactory,
            ILogger<SchemaCopier> logger)
        {
            _dataMoverSettings = dataMoverSettings;
            _policyFactory = policyFactory;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _policyFactory.DstPolicy.ExecuteAsync(async ct => await CreateDatabaseSchemaAsync(ct), token);

            var databaseObjects = (await ReadMetadataAsync(token))
                .Select((dto, index) => (dto, index))
                .ToList();

            var readQueue = new ConcurrentQueue<(DatabaseObjectDto dto, int index)>(databaseObjects);
            var scriptsQueue = new ConcurrentQueue<(string script, int index)>();

            async Task Read()
            {
                while (readQueue.TryDequeue(out var current) && !token.IsCancellationRequested)
                {
                    try
                    {
                        var swRead = Stopwatch.StartNew();
                        var script = await _policyFactory.SrcPolicy.ExecuteAsync(async ct =>
                            await GetScriptAsync(current.dto, ct), token);
                        swRead.Stop();
                        scriptsQueue.Enqueue((script, current.index));
                        _logger.LogDebug(
                            $"SchemaCopier. Read: {swRead.Elapsed.Milliseconds}");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "{@EventType}", "SchemaCopyRead_Failed");
                        throw;
                    }
                }
            }

            var readTasks = Enumerable.Range(0, _dataMoverSettings.ReadConcurrency)
                .Select(i => Read()).ToList();

            await Task.WhenAll(readTasks);
            if (readTasks.Any(t => t.IsFaulted))
            {
                throw new Exception("There are failed schema read tasks");
            }

            var scripts = scriptsQueue.OrderBy(i => i.index).Select(i => i.script).ToList();

            foreach (var script in scripts)
            {
                var swWrite = Stopwatch.StartNew();
                await _policyFactory.DstPolicy.ExecuteAsync(async ct =>
                    await ExecuteScriptAsync(SanitizeQueryScript(script), ct), token);
                swWrite.Stop();
                _logger.LogDebug(
                    $"SchemaCopier. Write: {swWrite.Elapsed.Milliseconds}");
            }
        }

        private async Task CreateDatabaseSchemaAsync(CancellationToken token)
        {
            var databaseMetadata = await ReadDatabaseMetadataAsync(token);

            var dstConnectionStringBuilder = new MySqlConnectionStringBuilder(_dataMoverSettings.ConnectionStrings.Dst);
            var databaseName = dstConnectionStringBuilder.Database;
            dstConnectionStringBuilder.Database = null;

            await using var connection = new MySqlConnection(dstConnectionStringBuilder.ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            var cmd = connection.CreateCommand();

            cmd.CommandType = CommandType.Text;

            cmd.CommandText = "";
            if (_dataMoverSettings.DropDatabase)
            {
                cmd.CommandText =
                    $"DROP DATABASE IF EXISTS `{databaseName}`;";
            }

            var collation = _dataMoverSettings.DatabaseCollation ?? databaseMetadata.Collation;
            var characterSet = _dataMoverSettings.DatabaseCharacterSet ?? databaseMetadata.CharacterSet;

            cmd.CommandText += $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET {characterSet} COLLATE {collation};";
            cmd.CommandTimeout = _dataMoverSettings.SchemaReadCommandTimeoutSeconds;

            await cmd.ExecuteNonQueryAsync(token);
        }

        private static string SanitizeQueryScript(string script)
        {
            return Regex.Replace(script, "DEFINER=`[a-zA-Z0-9_-]*`@`[a-zA-Z0-9%_-]*`", "");
        }

        private async Task ExecuteScriptAsync(string script, CancellationToken token)
        {
            await using var connection = new MySqlConnection(_dataMoverSettings.ConnectionStrings.Dst);
            await connection.OpenAsync(token).ConfigureAwait(false);

            await DisableChecks(token, connection);

            var cmd = connection.CreateCommand();

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = script;

            cmd.CommandTimeout = _dataMoverSettings.SchemaReadCommandTimeoutSeconds;

            await cmd.ExecuteNonQueryAsync(token);
        }

        private async Task DisableChecks(CancellationToken token, MySqlConnection connection)
        {
            var setSqlModeCmd = connection.CreateCommand();
            setSqlModeCmd.CommandType = CommandType.Text;
            setSqlModeCmd.CommandText = $"set unique_checks=0; set foreign_key_checks=0;";
            setSqlModeCmd.CommandTimeout = _dataMoverSettings.InsertCommandTimeoutSeconds;

            await setSqlModeCmd.ExecuteNonQueryAsync(token);
        }

        private async Task<string> GetScriptAsync(DatabaseObjectDto entityMetadata, CancellationToken token)
        {
            var sql = $"show create {entityMetadata.Type} `{entityMetadata.Name}`;";

            await using var connection = new MySqlConnection(_dataMoverSettings.ConnectionStrings.Src);
            await connection.OpenAsync(token).ConfigureAwait(false);

            var cmd = connection.CreateCommand();

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;

            cmd.CommandTimeout = _dataMoverSettings.SchemaReadCommandTimeoutSeconds;

            await using var reader = await cmd.ExecuteReaderAsync(token);

            if (await reader.ReadAsync(token))
            {
                await WaitForDebugDelay(token);

                var entityType = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(entityMetadata.Type);

                var createEntityScript = reader.GetString($"Create {entityType}");

                _logger.LogDebug($"DROP AND CREATE {entityType} `{entityMetadata.Name}`");

                return $"DROP {entityType} IF EXISTS `{entityMetadata.Name}`;{createEntityScript};";
            }

            throw new Exception("Script expected");
        }

        private async Task WaitForDebugDelay(CancellationToken token)
        {
            if (_dataMoverSettings.DebugDelaySeconds > 0)
            {
                _logger.LogDebug("{@EventType}", $"Delay for {_dataMoverSettings.DebugDelaySeconds} sec");
                await Task.Delay(TimeSpan.FromSeconds(_dataMoverSettings.DebugDelaySeconds), token);
            }
        }

        private async Task<DatabaseMatadataDto> ReadDatabaseMetadataAsync(CancellationToken token)
        {
            var db = new DatabaseGateway(_dataMoverSettings.ConnectionStrings.Src,
                _dataMoverSettings.SchemaReadCommandTimeoutSeconds, _policyFactory.SrcPolicy);

            var dbName = new MySqlConnectionStringBuilder(_dataMoverSettings.ConnectionStrings.Src).Database;

            var sql = @"select distinct DEFAULT_CHARACTER_SET_NAME as CharacterSet, DEFAULT_COLLATION_NAME as Collation
from information_schema.SCHEMATA
where SCHEMA_NAME = @databaseName;";

            return (await db.ReadAsync(default(Unused), sql,
                (_, collection) => collection.AddWithValue("databaseName", dbName),
                (_, reader) => new DatabaseMatadataDto
                {
                    CharacterSet = reader.GetString("CharacterSet"),
                    Collation = reader.GetString("Collation")
                },
                token)).Single();
        }

        private async Task<List<DatabaseObjectDto>> ReadMetadataAsync(CancellationToken token)
        {
            var db = new DatabaseGateway(_dataMoverSettings.ConnectionStrings.Src,
                _dataMoverSettings.SchemaReadCommandTimeoutSeconds, _policyFactory.SrcPolicy);

            var dbName = new MySqlConnectionStringBuilder(_dataMoverSettings.ConnectionStrings.Src).Database;

            var sql = @"select distinct TABLE_NAME as Name, 'table' as Type
from information_schema.TABLES
where TABLE_TYPE = 'BASE TABLE' and TABLE_SCHEMA = @databaseName
union
select TABLE_NAME as Name, 'view' as Type
from information_schema.TABLES
where TABLE_TYPE = 'VIEW' and TABLE_SCHEMA = @databaseName
union
select ROUTINE_NAME, 'function' as Type
from information_schema.ROUTINES
where ROUTINE_TYPE = 'FUNCTION' and ROUTINE_SCHEMA = @databaseName
union
select ROUTINE_NAME, 'procedure' as Type
from information_schema.ROUTINES
where ROUTINE_TYPE = 'PROCEDURE' and ROUTINE_SCHEMA = @databaseName
union
select TRIGGER_NAME, 'trigger' as Type
from information_schema.TRIGGERS
where TRIGGER_SCHEMA = @databaseName;";

            return await db.ReadAsync(default(Unused), sql,
                (_, collection) => collection.AddWithValue("databaseName", dbName),
                (_, reader) => new DatabaseObjectDto
                {
                    Name = reader.GetString("Name"),
                    Type = reader.GetString("Type")
                },
                token);
        }
    }
}
