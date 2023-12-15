using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Polly;

namespace Dodo.DataMover.DataManipulation
{
    public class DatabaseGateway
    {
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly AsyncPolicy _policy;

        public DatabaseGateway(string connectionString, int commandTimeoutSeconds, AsyncPolicy policy)
        {
            _connectionString = connectionString;
            _commandTimeoutSeconds = commandTimeoutSeconds;
            _policy = policy;
        }

        public async Task<List<T>> ReadAsync<T, TArg>(
            TArg arg,
            string sql,
            Action<TArg, MySqlParameterCollection> action,
            Func<TArg, DbDataReader, T> mapMetadata,
            CancellationToken token)
        {
            return await _policy.ExecuteAsync(async (ct) =>
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(ct);
                var cmd = connection.CreateCommand();
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = sql;
                action(arg, cmd.Parameters);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                var list = new List<T>();
                while (await reader.ReadAsync(token) && !token.IsCancellationRequested)
                {
                    list.Add(mapMetadata(arg, reader));
                }

                return list;
            }, token);
        }
    }
}
