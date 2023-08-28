using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeamBooking.Extensions.Logging.SqlServer
{
    [ProviderAlias("SqlServer")]
    public class SqlServerLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentQueue<LogMessage> _defaultQueue = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<LogMessage>> _tenantQueues =
            new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly IOptions<SqlServerLoggerOptions> _options;
        private readonly Task _outputTask;

        internal IExternalScopeProvider? ScopeProvider { get; private set; }

        public SqlServerLoggerProvider(IOptions<SqlServerLoggerOptions> options)
        {
            _options = options;
            _outputTask = Task.Run(ProcessLogQueuesAsync);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SqlServerLogger(this, categoryName, _options.Value);
        }

        internal void AddMessage(LogMessage message)
        {
            var queue = message.Tenant is null
                ? _defaultQueue
                : _tenantQueues.GetOrAdd(message.Tenant, _ => new ConcurrentQueue<LogMessage>());
            queue.Enqueue(message);
        }

        private async Task ProcessLogQueuesAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var tasks = new List<Task>(1 + _tenantQueues.Count)
                {
                    ProcessQueueAsync(null, _defaultQueue, _cancellationTokenSource.Token)
                };

                foreach (var (tenant, queue) in _tenantQueues)
                {
                    tasks.Add(ProcessQueueAsync(tenant, queue, _cancellationTokenSource.Token));
                }

                await Task.WhenAll(tasks);
                await Task.Delay(_options.Value.BatchInterval, _cancellationTokenSource.Token);
            }
        }

        private async Task ProcessQueueAsync(
            string? tenant,
            ConcurrentQueue<LogMessage> queue,
            CancellationToken cancellationToken
        )
        {
            if (queue.IsEmpty)
            {
                return;
            }

            var batch = new List<LogMessage>(queue.Count);
            while (queue.TryDequeue(out var message))
            {
                batch.Add(message);
            }

            if (batch.Count == 0)
            {
                return;
            }

            try
            {
                await StoreBatchAsync(tenant, batch, _cancellationTokenSource.Token);
            }
            catch (Exception e) when (tenant is not null)
            {
                var metadataValues = _options.Value.MetaMappings
                    .Select(x => x.DefaultValue)
                    .ToList();
                AddMessage(
                    new LogMessage(
                        null,
                        nameof(SqlServerLoggerProvider),
                        $"Unable to store a {batch.Count} message batch for tenant {tenant}, got error: {e.Message}",
                        LogLevel.Error,
                        metadataValues
                    )
                );
            }
        }

        private async Task StoreBatchAsync(
            string? tenant,
            IEnumerable<LogMessage> batch,
            CancellationToken cancellationToken
        )
        {
            var table = CreateTable(batch);

            if (table.Rows.Count == 0)
            {
                return;
            }

            var connectionString = _options.Value?.GetConnectionString?.Invoke(tenant);
            if (connectionString is null)
            {
                return;
            }

            await using var connection = new SqlConnection(connectionString);
            using var sqlBulkCopy = new SqlBulkCopy(
                connection,
                SqlBulkCopyOptions.Default,
                externalTransaction: null
            )
            {
                DestinationTableName = _options.Value?.TableName,
                BulkCopyTimeout = 5, // seconds
            };

            foreach (DataColumn column in table.Columns)
            {
                sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            var wasClosed = connection.State == ConnectionState.Closed;
            try
            {
                if (wasClosed)
                {
                    connection.Open();
                }

                await sqlBulkCopy.WriteToServerAsync(table, cancellationToken);
            }
            finally
            {
                if (wasClosed)
                {
                    connection.Close();
                }
            }
        }

        private DataTable CreateTable(IEnumerable<LogMessage> batch)
        {
            var table = new DataTable()
            {
                Locale = CultureInfo.InvariantCulture,
                Columns =
                {
                    new DataColumn(_options.Value.LoggerColumnName, typeof(string)),
                    new DataColumn(_options.Value.MessageColumnName, typeof(string)),
                    new DataColumn(_options.Value.LogLevelColumnName),
                }
            };

            foreach (var mapping in _options.Value.MetaMappings)
            {
                table.Columns.Add(new DataColumn(mapping.ColumnName));
            }

            foreach (var message in batch)
            {
                table.Rows.Add(ConvertToRow(message));
            }

            return table;
        }

        private object[] ConvertToRow(LogMessage message)
        {
            var row = new object[3 + _options.Value.MetaMappings.Count];

            row[0] = message.Logger;
            row[1] = message.FormattedMessage;
            row[2] = _options.Value.MapLogLevel(message.LogLevel);
            message.MetadataValues.CopyTo(row, 3);

            return row;
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            ScopeProvider = scopeProvider;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _outputTask.Wait(_options.Value.BatchInterval);
            }
            catch (AggregateException e) when (e.InnerException is TaskCanceledException) { }
            catch (TaskCanceledException) { }
        }
    }
}
