using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dodo.DataMover.Common.Collections;
using Dodo.DataMover.Common.Text;
using Dodo.DataMover.DataManipulation.Models;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace Dodo.DataMover.DataManipulation
{
    public class DatabasePublisher
    {
        private readonly DataMoverSettings _dataMoverSettings;
        private readonly ILogger<DatabasePublisher> _logger;
        private readonly PolicyFactory _policyFactory;

        public DatabasePublisher(
            ILogger<DatabasePublisher> logger,
            PolicyFactory policyFactory,
            DataMoverSettings dataMoverSettings)
        {
            _logger = logger;
            _policyFactory = policyFactory;
            _dataMoverSettings = dataMoverSettings;
        }

        public async Task PublishAsync(InsertCommand insertCommand, CancellationToken token)
        {
            var partitionSize = GetPartitionSize(insertCommand.Batch.Columns);

            foreach (var partition in insertCommand.Batch.Rows.StatelessPartition(partitionSize))
            {
                await _policyFactory
                    .DstPolicy
                    .ExecuteAsync(ct => InsertBatchInternal(insertCommand, partition, ct), token);
            }
        }

        private async Task InsertBatchInternal(
            InsertCommand insertCommand,
            IReadOnlyList<List<object>> partition,
            CancellationToken token)
        {
            var partitionList = partition.ToList();

            await using var connection =
                new MySqlConnection(_dataMoverSettings.ConnectionStrings.Dst);
            await connection.OpenAsync(token).ConfigureAwait(false);

            await DisableChecks(token, connection);

            if (_dataMoverSettings.SqlMode != null)
            {
                await SetConnectionSessionSqlMode(token, connection);
            }

            var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = GenerateSql(insertCommand.TableName, insertCommand.Batch.Columns,
                partitionList.Count, _dataMoverSettings.InsertIgnore);
            cmd.CommandTimeout = _dataMoverSettings.InsertCommandTimeoutSeconds;

            AddCommandParameters(cmd, partitionList);

            await WaitForDebugDelay(token);

            _logger.LogTrace("{@EventType} {@Query}", "Insert_Started", cmd.CommandText);
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            cmd.Transaction = tx;
            try
            {
                await cmd.ExecuteNonQueryAsync(token);
                await tx.CommitAsync(token);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{@EventType} {@CommandText}", "Insert_Failed", cmd.CommandText);
                await tx.RollbackAsync(token);
                throw;
            }

            _logger.LogDebug("{@EventType}", "Insert_Completed");
        }

        private async Task WaitForDebugDelay(CancellationToken token)
        {
            if (_dataMoverSettings.DebugDelaySeconds > 0)
            {
                _logger.LogDebug("{@EventType}", $"Delay for {_dataMoverSettings.DebugDelaySeconds} sec");
                await Task.Delay(TimeSpan.FromSeconds(_dataMoverSettings.DebugDelaySeconds), token);
            }
        }

        private static void AddCommandParameters(
            MySqlCommand cmd,
            List<List<object>> partitionList)
        {
            var parameterNumber = 1;
            foreach (var row in partitionList)
            {
                foreach (var columnValue in row)
                {
                    cmd.Parameters.AddWithValue(MakeParamName(parameterNumber), columnValue);
                    parameterNumber++;
                }
            }
        }

        private async Task DisableChecks(CancellationToken token, MySqlConnection connection)
        {
            var setSqlModeCmd = connection.CreateCommand();
            setSqlModeCmd.CommandType = CommandType.Text;
            setSqlModeCmd.CommandText = $"set unique_checks=0; set foreign_key_checks=0;";
            setSqlModeCmd.CommandTimeout = _dataMoverSettings.InsertCommandTimeoutSeconds;

            await setSqlModeCmd.ExecuteNonQueryAsync(token);
        }

        private async Task SetConnectionSessionSqlMode(CancellationToken token, MySqlConnection connection)
        {
            var setSqlModeCmd = connection.CreateCommand();
            setSqlModeCmd.CommandType = CommandType.Text;
            setSqlModeCmd.CommandText = $"set session sql_mode = '{_dataMoverSettings.SqlMode}'";
            setSqlModeCmd.CommandTimeout = _dataMoverSettings.InsertCommandTimeoutSeconds;

            await setSqlModeCmd.ExecuteNonQueryAsync(token);
        }

        private static string MakeParamName(int n)
        {
            return $"@p{n}";
        }

        public static string GenerateSql(string tableName, List<Column> columns, int rowCount, bool insertIgnore)
        {
            var parameterNumber = 1;
            var columnCount = columns.Count;
            var queryBuilder = new StringBuilder();

            queryBuilder
                .Append(insertIgnore
                    ? $"insert ignore into `{tableName}` "
                    : $"insert into `{tableName}` ")
                .Append("(")
                .AppendJoin(",", columns.Select(x => $"`{x.Name}`"))
                .Append(")")
                .Append(" values ")
                .AppendJoin(
                    ",",
                    Enumerable.Repeat(0, rowCount),
                    (rowBuilder, _) => rowBuilder
                        .Append("(")
                        .AppendJoin(
                            ",",
                            Enumerable.Repeat(0, columnCount),
                            (paramBuilder, _) => paramBuilder.Append("@p").Append(parameterNumber++))
                        .Append(")"));

            var commandText = queryBuilder.ToString();
            return commandText;
        }

        private int GetPartitionSize(List<Column> columns)
        {
            const int maxParametersCount = 10_000;
            return maxParametersCount / columns.Count;
        }
    }
}
