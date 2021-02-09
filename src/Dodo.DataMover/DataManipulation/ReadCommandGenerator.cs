using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dodo.DataMover.DataManipulation.Models;
using Microsoft.Extensions.Logging;

namespace Dodo.DataMover.DataManipulation
{
    public class ReadCommandGenerator
    {
        private readonly ILogger<ReadCommandGenerator> _logger;

        private readonly SourceSchemaReader _schemaReader;

        public ReadCommandGenerator(ILogger<ReadCommandGenerator> logger, SourceSchemaReader schemaReader)
        {
            _logger = logger;
            _schemaReader = schemaReader;
        }

        public async IAsyncEnumerable<ReadCommand> GetReadCommandsAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            _logger.LogInformation("{@EventType}",
                "ReadSchema_Started");

            var tables = await _schemaReader.GetTablesAsync(ct);
            if (!tables.Any())
            {
                _logger.LogWarning("There are no tables in src database schema");
            }

            _logger.LogInformation("{@EventType}",
                "ReadSchema_Completed");

            foreach (var tableSchema in tables)
            {
                ct.ThrowIfCancellationRequested();

                await foreach (var readCommand in _schemaReader
                    .GetReadCommandsAsync(tableSchema, ct).WithCancellation(ct))
                {
                    yield return readCommand;
                }
            }
        }
    }
}
