using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TeamBooking.Extesions.Logging.SqlServer
{
    internal class SqlServerLogger : ILogger
    {
        private readonly SqlServerLoggerProvider _provider;
        private readonly string _name;
        private readonly SqlServerLoggerOptions _options;

        public SqlServerLogger(SqlServerLoggerProvider provider, string name, SqlServerLoggerOptions options)
        {
            _provider = provider;
            _name = name;
            _options = options;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return _provider.ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var metadata = new Dictionary<string, object>();

            _provider.ScopeProvider?.ForEachScope((scopeState, _) =>
            {
                if (scopeState is IEnumerable<KeyValuePair<string, object>> enumerable)
                {
                    foreach (var (key, value) in enumerable)
                    {
                        metadata[key] = value;
                    }
                }
            }, state);

            var metadataValues = new List<object>();

            foreach (var mapping in _options.MetaMappings)
            {
                var value = metadata.GetValueOrDefault(mapping.LogTemplateKey, mapping.DefaultValue);
                metadataValues.Add(value ?? DBNull.Value);
            }

            var systemId = (int)metadata.GetValueOrDefault("SystemId", 0);

            _provider.AddMessage(new LogMessage(systemId, _name, formatter(state, exception), logLevel, metadataValues));
        }
    }
}
