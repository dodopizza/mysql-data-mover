using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dodo.DataMover.DataManipulation.DatabaseMapping;
using Dodo.DataMover.DataManipulation.Models;
using MySql.Data.MySqlClient;

namespace Dodo.DataMover.DataManipulation
{
    public class SourceSchemaReader
    {
        private readonly PolicyFactory _policyFactory;
        private readonly DataMoverSettings _dataMoverSettings;

        private ConcurrentDictionary<(string tableName, bool hasFrom), string> _keyRangeSelectSql =
            new ConcurrentDictionary<(string tableName, bool hasFrom), string>();

        public SourceSchemaReader(PolicyFactory policyFactory, DataMoverSettings dataMoverSettings)
        {
            _policyFactory = policyFactory;
            _dataMoverSettings = dataMoverSettings;
        }

        public async Task<List<TableSchema>> GetTablesAsync(CancellationToken token)
        {
            var db = new DatabaseGateway(
                _dataMoverSettings.ConnectionStrings.Src,
                _dataMoverSettings.SchemaReadCommandTimeoutSeconds,
                _policyFactory.SrcPolicy);

            var databaseName = new MySqlConnectionStringBuilder(_dataMoverSettings.ConnectionStrings.Src).Database;

            var tables = await GetTableRecordsAsync(db, databaseName, token);
            var columns = await GetColumnsRecordsAsync(db, databaseName, token);

            var tableColumns = tables.Join(
                columns,
                table => table.Name,
                column => column.TableName,
                (table, column) => new
                {
                    Table = table,
                    Column = column
                }).GroupBy(x => x.Table).Select(x => new TableSchema
            {
                Name = x.Key.Name,
                Columns = x.Select(y => new Column
                {
                    Name = y.Column.Name,
                    DataType = y.Column.DataType,
                    ColumnType = y.Column.ColumnType,
                    PkOrdinalPosition = y.Column.PkOrdinalPosition
                }).ToList()
            }).ToList();
            return tableColumns;
        }

        public async IAsyncEnumerable<ReadCommand> GetReadCommandsAsync(
            TableSchema tableSchema,
            [EnumeratorCancellation] CancellationToken ct)
        {
            List<object> from = null, to;
            var tableName = tableSchema.Name;
            var pkNames = tableSchema.Columns
                .Where(c => c.PkOrdinalPosition != null)
                .OrderBy(c => c.PkOrdinalPosition)
                .Select(c => c.Name)
                .ToList();

            if (pkNames.Count == 0)
            {
                yield return new ReadCommand(tableSchema, null, null);
                yield break;
            }

            long? limit = GetLimitOverride(tableName) ?? _dataMoverSettings.Limit;
            foreach (var currentBatchSize in GetBatchSizes(limit, _dataMoverSettings.ReadBatchSize))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                (from, to) = await MapKeySpaceAsync(tableName, currentBatchSize, from, pkNames, ct);
                if (from == null)
                {
                    break;
                }

                yield return new ReadCommand(tableSchema, from, to);
                from = to;
                if (to == null)
                {
                    break;
                }
            }
        }

        private long? GetLimitOverride(string tableName) =>
            _dataMoverSettings.LimitOverrides
                .Where(entry => Regex.IsMatch(tableName, entry.Key))
                .Select(entry => entry.Value)
                .FirstOrDefault();

        string GenerateKeyRangeSelectSql(string tableName, List<object> from, List<string> pkNames)
        {
            return _keyRangeSelectSql.GetOrAdd((tableName, from != null), (key, pkNames) =>
            {
                var pkColumnsClause = string.Join(", ", pkNames.Select(n => $"`{n}`"));
                var fromClause = SqlUtil.MakeLexicalGreaterOrEqualComparision(pkNames,
                    Enumerable.Range(0, pkNames.Count).Select(i => $"p{i}").ToList());
                var whereClause = !key.hasFrom
                    ? ""
                    : $"where {fromClause}";
                return @$"(select {pkColumnsClause}, 1 as ordinal
                from `{key.tableName}` {whereClause}
                order by {pkColumnsClause}
                    limit 1)
                union
                    (select {pkColumnsClause}, 2 as ordinal
                from `{key.tableName}` {whereClause}
                order by {pkColumnsClause}
                    limit @batchSize, 1)";
            }, pkNames);
        }

        async Task<(List<object> @from, List<object> to)> MapKeySpaceAsync(
            string tableName,
            long batchSize,
            List<object> @from,
            List<string> pkNames,
            CancellationToken token)
        {
            var db = new DatabaseGateway(
                _dataMoverSettings.ConnectionStrings.Src,
                _dataMoverSettings.DataReadCommandTimeoutSeconds,
                _policyFactory.SrcPolicy);

            var fromTo = (await db.ReadAsync((batchSize, from, pkNames),
                    GenerateKeyRangeSelectSql(tableName, from, pkNames),
                    (a, cmd) =>
                    {
                        if (a.from != null)
                        {
                            for (var i = 0; i < a.pkNames.Count; i++)
                            {
                                cmd.AddWithValue($"p{i}", a.from[i]);
                            }
                        }

                        cmd.AddWithValue("batchSize", a.batchSize);
                    },
                    (a, reader) => (
                        pk: a.pkNames.Select(reader.GetValue).ToList(),
                        ordinal: reader.GetInt32("ordinal")),
                    token
                ))
                .OrderBy(i => i.ordinal).ToList();
            switch (fromTo.Count)
            {
                case 0:
                    return (null, null);
                case 1:
                    return (fromTo[0].pk, null);
                case 2:
                    return (fromTo[0].pk, fromTo[1].pk);
                default:
                    throw new Exception("Key range query resulted in an unexpected number of keys");
            }
        }

        public static IEnumerable<long> GetBatchSizes(long? limit, long batchSize)
        {
            if (batchSize == 0)
            {
                yield break;
            }

            if (limit == null)
            {
                while (true)
                {
                    yield return batchSize;
                }
            }

            if (limit < batchSize)
            {
                yield return limit.Value;
            }

            var count = 0L;
            for (; count <= limit - batchSize; count += batchSize)
            {
                yield return batchSize;
            }

            if (count != 0 && count < limit)
            {
                yield return (long) limit - count;
            }
        }

        private async Task<List<TableDto>> GetTableRecordsAsync(
            DatabaseGateway db,
            string databaseName,
            CancellationToken token)
        {
            return (await db.ReadAsync(databaseName,
                    "select distinct TABLE_NAME from information_schema.TABLES where TABLE_TYPE = 'BASE TABLE' and TABLE_SCHEMA = @databaseName",
                    (dbName, parameters) => parameters.AddWithValue("databaseName", dbName),
                    (_, reader) => new TableDto
                    {
                        Name = reader.GetString("TABLE_NAME"),
                    },
                    token
                ))
                .Where(x => TableCriteriaMatches(
                    x.Name,
                    _dataMoverSettings.IncludeTableRegexes,
                    _dataMoverSettings.ExcludeTableRegexes))
                .ToList();
        }

        public static bool TableCriteriaMatches(
            string tableName,
            string[] includeTableRegexes,
            string[] excludeTableRegexes)
        {
            return includeTableRegexes.Any(pattern => Regex.IsMatch(tableName, pattern))
                   || !excludeTableRegexes.Any(pattern => Regex.IsMatch(tableName, pattern));
        }

        private Task<List<ColumnDto>> GetColumnsRecordsAsync(
            DatabaseGateway db,
            string databaseName,
            CancellationToken token)
        {
            return db.ReadAsync(databaseName,
                @"SELECT DISTINCT c.TABLE_NAME, c.COLUMN_NAME, c.COLUMN_TYPE, c.DATA_TYPE, c.EXTRA, c.ORDINAL_POSITION as COLUMN_ORDINAL_POSITION, k.ORDINAL_POSITION as PK_ORDINAL_POSITION
FROM information_schema.columns c
LEFT JOIN information_schema.key_column_usage k
ON c.TABLE_NAME = k.TABLE_NAME and c.TABLE_SCHEMA = k.TABLE_SCHEMA and c.COLUMN_NAME = k.COLUMN_NAME and k.CONSTRAINT_NAME='PRIMARY'
LEFT JOIN information_schema.table_constraints t
ON k.TABLE_NAME = t.TABLE_NAME and k.TABLE_SCHEMA = t.TABLE_SCHEMA and t.CONSTRAINT_NAME = k.CONSTRAINT_NAME and t.CONSTRAINT_TYPE='PRIMARY KEY'
WHERE  c.TABLE_SCHEMA=@databaseName and c.EXTRA not in ('VIRTUAL GENERATED', 'STORED GENERATED');",
                (dbName, parameters) => parameters.AddWithValue("databaseName", dbName),
                (_, reader) => new ColumnDto
                {
                    TableName = reader.GetString("TABLE_NAME"),
                    Name = reader.GetString("COLUMN_NAME"),
                    ColumnType = reader.GetString("COLUMN_TYPE"),
                    DataType = reader.GetString("DATA_TYPE"),
                    PkOrdinalPosition = reader.IsDBNull("PK_ORDINAL_POSITION")
                        ? null
                        : (int?) reader.GetInt32("PK_ORDINAL_POSITION")
                },
                token);
        }
    }
}
