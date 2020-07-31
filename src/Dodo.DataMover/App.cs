using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dodo.DataMover.DataManipulation;
using Dodo.DataMover.DataManipulation.Models;
using Microsoft.Extensions.Logging;

namespace Dodo.DataMover
{
    public class App
    {
        private readonly DataMoverSettings _dataMoverSettings;
        private readonly SourceDataReader _sourceDataReader;
        private readonly ILogger<App> _logger;
        private readonly DatabasePublisher _publisher;
        private readonly ReadCommandGenerator _readCommandGenerator;
        private readonly SchemaCopier _schemaCopier;

        public App(
            ILogger<App> logger,
            DataMoverSettings dataMoverSettings,
            SourceDataReader sourceDataReader,
            DatabasePublisher publisher,
            ReadCommandGenerator readCommandGenerator,
            SchemaCopier schemaCopier)
        {
            _logger = logger;
            _dataMoverSettings = dataMoverSettings;
            _sourceDataReader = sourceDataReader;
            _publisher = publisher;
            _readCommandGenerator = readCommandGenerator;
            _schemaCopier = schemaCopier;
        }

        public async Task RunAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource(
                TimeSpan.FromMinutes(_dataMoverSettings.JobTimeoutMinutes));
            var cancellationToken = cancellationTokenSource.Token;

            var applicationSettingsLog = GetApplicationSettingsLog();

            _logger.LogInformation(applicationSettingsLog);

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("mysql-data-mover job {@EventType}",
                "Job_Started");

            try
            {
                if (_dataMoverSettings.CreateSchema)
                {
                    await CopySchemaAsync(cancellationToken);
                }

                await ProcessPipelinesAsync(cancellationToken, requester =>
                {
                    _logger.LogInformation("{@EventType} {@RequestedBy}", "Pipeline_Cancelled", requester);
                    cancellationTokenSource.Cancel();
                });

                _logger.LogInformation("{@EventType} {@ElapsedSeconds}",
                    "Job_Completed", sw.Elapsed.TotalSeconds);
            }
            catch
            {
                _logger.LogError("Job failed, bubbling up exception. {@EventType} {@ElapsedSeconds}",
                    "Job_Failed", sw.Elapsed.TotalSeconds);
                throw;
            }
        }

        private string GetApplicationSettingsLog()
        {
            var sb = new StringBuilder();

            var dataMoverSettings = JsonSerializer.Serialize(_dataMoverSettings);
            sb.AppendLine("mysql-data-mover Application_Started");
            sb.AppendLine($"mysql-data-mover settings {dataMoverSettings}");

            return sb.ToString();
        }

        private async Task CopySchemaAsync(CancellationToken cancellationToken)
        {
            await _schemaCopier.ExecuteAsync(cancellationToken);
        }

        private async Task ProcessPipelinesAsync(CancellationToken ct, Action<string> cancelPipeline)
        {
            const int queueLengthQuantifier = 5;
            var readCommandChannel =
                Channel.CreateBounded<ReadCommand>(_dataMoverSettings.ReadConcurrency * queueLengthQuantifier);
            var insertCommandChannel =
                Channel.CreateBounded<InsertCommand>(_dataMoverSettings.InsertConcurrency * queueLengthQuantifier);

            async Task RunCommandGeneratorWorker()
            {
                try
                {
                    _logger.LogDebug("{@EventType}", "Pipeline_SchemaReadStarted");
                    await foreach (var readCommand in _readCommandGenerator
                        .GetReadCommandsAsync(ct).WithCancellation(ct))
                    {
                        await readCommandChannel.Writer.WaitToWriteAsync(ct);
                        await readCommandChannel.Writer.WriteAsync(readCommand, ct);
                    }

                    _logger.LogDebug("{@EventType}", "Pipeline_SchemaReadCompleted");
                }
                catch (Exception e)
                {
                    cancelPipeline(nameof(RunCommandGeneratorWorker));
                    _logger.LogError(e, "{@EventType}", "Pipeline_SchemaReadFailed");
                    throw;
                }
                finally
                {
                    if (!readCommandChannel.Writer.TryComplete())
                    {
                        _logger.LogWarning("Failed to complete writing to readCommandChannel");
                    }
                }
            }

            async Task RunReadWorker(int n)
            {
                while (await readCommandChannel.Reader.WaitToReadAsync(ct) && !ct.IsCancellationRequested)
                {
                    object debugPayload = null;
                    try
                    {
                        var command = await readCommandChannel.Reader.ReadAsync(ct);
                        debugPayload = new
                        {
                            TableName = command.TableSchema.Name,
                            command.FromPrimaryKey,
                            command.ToPrimaryKey
                        };
                        _logger.LogDebug("{@EventType} {@Payload}", "Pipeline_ReadStarted", debugPayload);
                        var batch = await _sourceDataReader.ReadBatchAsync(command, ct);

                        await insertCommandChannel.Writer.WaitToWriteAsync(ct);
                        await insertCommandChannel.Writer.WriteAsync(new InsertCommand
                        {
                            TableName = command.TableSchema.Name,
                            Batch = batch
                        }, ct);

                        _logger.LogDebug("{@EventType} {@Payload}", "Pipeline_ReadCompleted", debugPayload);
                    }
                    catch (ChannelClosedException)
                    {
                        _logger.LogDebug(
                            $"Catching channel 'readCommandChannel' closed exception by read worker {n}. This should be considered normal.");
                    }
                    catch (Exception e)
                    {
                        cancelPipeline(nameof(RunReadWorker));
                        _logger.LogError(e, "{@EventType} {@Payload}, bubbling up exception",
                            "Pipeline_ReadFailed", debugPayload);
                        throw;
                    }
                }
            }

            async Task RunInsertWorker(int n)
            {
                while (await insertCommandChannel.Reader.WaitToReadAsync(ct) && !ct.IsCancellationRequested)
                {
                    object debugPayload = null;
                    try
                    {
                        var command = await insertCommandChannel.Reader.ReadAsync(ct);
                        debugPayload = new
                        {
                            command.TableName,
                        };
                        _logger.LogDebug("{@EventType} {@Payload}", "Pipeline_InsertStarted", debugPayload);
                        await _publisher.PublishAsync(command, ct);
                        _logger.LogDebug("{@EventType} {@Payload}", "Pipeline_InsertCompleted", debugPayload);
                    }
                    catch (ChannelClosedException)
                    {
                        _logger.LogDebug(
                            $"Catching channel 'insertCommandChannel' closed exception by writer {n}. This should be considered normal.");
                    }
                    catch (Exception e)
                    {
                        cancelPipeline(nameof(RunInsertWorker));
                        _logger.LogError(e, "{@EventType} {@Payload}, bubbling up exception",
                            "Pipeline_InsertFailed", debugPayload);
                        throw;
                    }
                }
            }

            var allPipelineTasks = new List<Task>();
            var commandGeneratorWorker = RunCommandGeneratorWorker();
            allPipelineTasks.Add(commandGeneratorWorker);
            var readWorkers = Enumerable.Range(0, _dataMoverSettings.ReadConcurrency)
                .Select(RunReadWorker)
                .ToList();
            allPipelineTasks.AddRange(readWorkers);

            var insertWorkers = Enumerable.Range(0, _dataMoverSettings.InsertConcurrency)
                .Select(RunInsertWorker)
                .ToList();
            allPipelineTasks.AddRange(insertWorkers);

            var firstStage = readWorkers.Concat(new[] {commandGeneratorWorker}).ToList();

            try
            {
                await Task.WhenAll(firstStage);
            }
            catch
            {
            }

            if (!insertCommandChannel.Writer.TryComplete())
            {
                _logger.LogWarning("Failed to complete writing to insertCommandChannel");
            }

            try
            {
                await Task.WhenAll(insertWorkers);
            }
            catch
            {
            }

            if (allPipelineTasks.Any(t => t.IsFaulted))
            {
                _logger.LogError("{@EventType}",
                    "Pipeline_Failed");
                throw new Exception("Pipeline has errors");
            }

            _logger.LogInformation("{@EventType}",
                "Pipeline_Finished");
        }
    }
}
