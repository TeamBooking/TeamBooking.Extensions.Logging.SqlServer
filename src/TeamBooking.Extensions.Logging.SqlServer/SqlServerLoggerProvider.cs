using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace TeamBooking.Extesions.Logging.SqlServer
{
    internal class SqlServerLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<LogMessage>> _messageQueues = new ConcurrentDictionary<int, ConcurrentQueue<LogMessage>>();
        private readonly List<LogMessage> _currentBatch = new List<LogMessage>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly IOptions<SqlServerLoggerOptions> _options;
        private readonly Task _outputTask;

        internal IExternalScopeProvider ScopeProvider { get; private set; }

        public SqlServerLoggerProvider(IOptions<SqlServerLoggerOptions> options)
        {
            _options = options;
            _outputTask = Task.Run(ProcessLogQueue);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SqlServerLogger(this, categoryName, _options.Value);
        }

        internal void AddMessage(LogMessage message)
        {
            var queue = _messageQueues.GetOrAdd(message.SystemId, _ => new ConcurrentQueue<LogMessage>());
            queue.Enqueue(message);
        }

        private async Task ProcessLogQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                foreach (var (systemId, queue) in _messageQueues)
                {
                    while (queue.TryDequeue(out var message))
                    {
                        _currentBatch.Add(message);
                    }

                    if (_currentBatch.Count > 0)
                    {
                        await StoreBatchAsync(systemId, _currentBatch, _cancellationTokenSource.Token);
                        _currentBatch.Clear();
                    }
                }

                await Task.Delay(_options.Value.BatchInterval, _cancellationTokenSource.Token);
            }
        }

        private async Task StoreBatchAsync(int systemId, IEnumerable<LogMessage> batch, CancellationToken cancellationToken)
        {
            var table = CreateTable(batch);

            if (table.Rows.Count == 0)
            {
                return;
            }

            var connectionString = _options.Value?.GetConnectionString(systemId);

            if (connectionString is null)
            {
                return;
            }

            using var connection = new SqlConnection(connectionString);
            using var sqlBulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, externalTransaction: null)
            {
                DestinationTableName = _options.Value.TableName
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
                Locale = CultureInfo.InvariantCulture
            };

            table.Columns.Add(new DataColumn(_options.Value.LoggerColumnName, typeof(string)));
            table.Columns.Add(new DataColumn(_options.Value.MessageColumnName, typeof(string)));
            table.Columns.Add(new DataColumn(_options.Value.LogLevelColumnName));

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
            catch (TaskCanceledException)
            {
            }
        }
    }
}
