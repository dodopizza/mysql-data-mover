using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dodo.DataMover.DataManipulation.Models;
using MySqlConnector;

namespace Dodo.DataMover.DataManipulation
{
    public class SourceDataReader
    {
        private readonly PolicyFactory _policyFactory;
        private readonly DataMoverSettings _dataMoverSettings;

        public SourceDataReader(PolicyFactory policyFactory, DataMoverSettings dataMoverSettings)
        {
            _policyFactory = policyFactory;
            _dataMoverSettings = dataMoverSettings;
        }

        public async Task<Batch> ReadBatchAsync(ReadCommand command, CancellationToken token)
        {
            var db = new DatabaseGateway(
                _dataMoverSettings.ConnectionStrings.Src,
                _dataMoverSettings.DataReadCommandTimeoutSeconds,
                _policyFactory.SrcPolicy);
            var skipInsertColumnsNames = GetSkippedColumnsNames(command.TableSchema.Name);
            var columns = command.TableSchema.Columns.Where(x => !skipInsertColumnsNames.Contains(x.Name)).ToList();
            var rows = await db.ReadAsync((command, columns), BuildQuery(command, columns),
                (a, p) => AddQueryParameters(a.command, p), (a, r) => MapRow(a.columns, r), token);
            return new Batch
            {
                Columns = columns,
                Rows = rows
            };
        }

        List<object> MapRow(List<Column> columns, DbDataReader reader)
        {
            var l = new List<object>();
            foreach (var column in columns)
            {
                l.Add(reader.GetValue(column.Name));
            }

            return l;
        }

        private HashSet<string> GetSkippedColumnsNames(string tableName)
        {
            return new HashSet<string>(
                _dataMoverSettings.SkipColumnsRegexes
                    .Where(pair => Regex.IsMatch(tableName, pair.Key))
                    .SelectMany(pair => pair.Value));
        }

        private static List<string> GetPkNames(TableSchema commandTableSchema)
        {
            var pkNames = commandTableSchema.Columns
                .Where(c => c.PkOrdinalPosition != null)
                .OrderBy(c => c.PkOrdinalPosition)
                .Select(c => c.Name)
                .ToList();
            return pkNames;
        }

        private static void AddQueryParameters(ReadCommand command, MySqlParameterCollection parameters)
        {
            var pkNames = GetPkNames(command.TableSchema);
            for (var i = 0; i < pkNames.Count; i++)
            {
                parameters.AddWithValue($"from_{i}", command.FromPrimaryKey[i]);
            }

            if (command.ToPrimaryKey != null)
            {
                for (var i = 0; i < pkNames.Count; i++)
                {
                    parameters.AddWithValue($"to_{i}", command.ToPrimaryKey[i]);
                }
            }
        }

        public static string BuildQuery(ReadCommand command, List<Column> columns)
        {
            var columnNames = string.Join(",", columns.Select(x => $"`{x.Name}`"));
            var queryBuilder = new StringBuilder($"select {columnNames} from `{command.TableSchema.Name}`");
            var pkNames = GetPkNames(command.TableSchema);

            if (pkNames.Count > 0)
            {
                var fromClause = SqlUtil
                    .MakeLexicalGreaterOrEqualComparision(
                        pkNames,
                        Enumerable.Range(0, pkNames.Count).Select(i => $"from_{i}").ToList()
                    );
                queryBuilder.Append($" where ({fromClause})");

                if (command.ToPrimaryKey != null)
                {
                    var toClause = SqlUtil
                        .MakeLexicalLessThanComparision(
                            pkNames,
                            Enumerable.Range(0, pkNames.Count).Select(i => $"to_{i}").ToList());
                    queryBuilder
                        .Append($" and ({toClause})");
                }
            }

            return queryBuilder.ToString();
        }
    }
}
