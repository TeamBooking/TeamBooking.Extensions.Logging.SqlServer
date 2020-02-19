using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamBooking.Extesions.Logging.SqlServer
{
    internal class SqlServerLogger : ILogger
    {
        private readonly SqlServerLoggerProvider _provider;
        private readonly string _name;
        private readonly SqlServerLoggerOptions _options;
        private readonly Stack<StateScope> _scopes = new Stack<StateScope>();

        public SqlServerLogger(SqlServerLoggerProvider provider, string name, SqlServerLoggerOptions options)
        {
            _provider = provider;
            _name = name;
            _options = options;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            var scope = new StateScope(this, state);
            _scopes.Push(scope);

            return scope;
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

            var metadata = new List<object>();

            foreach (var mapping in _options.MetaMappings)
            {
                var value = GetMetadataValue(mapping.LogTemplateKey, state);
                metadata.Add(value ?? mapping.DefaultValue);
            }

            var systemId = (int?)GetMetadataValue("SystemId", state) ?? 0;

            _provider.AddMessage(new LogMessage(systemId, _name, formatter(state, exception), logLevel, metadata));
        }

        private object GetMetadataValue<TState>(string key, TState state)
        {
            var value = GetMetadataValue(state, key);

            if (value is object)
            {
                return value;
            }

            foreach (var scope in _scopes.Reverse())
            {
                value = GetMetadataValue(scope.State, key);

                if (value is object)
                {
                    return value;
                }
            }

            return null;
        }

        private static object GetMetadataValue(object state, string key)
        {
            if (state is IEnumerable<KeyValuePair<string, object>> enumerable)
            {
                return enumerable.FirstOrDefault(x => x.Key == key).Value;
            }

            var property = state.GetType().GetProperty(key);

            if (property is object)
            {
                return property.GetValue(state);
            }

            return null;
        }

        private class StateScope : IDisposable
        {
            private readonly SqlServerLogger _logger;

            public object State { get; }

            public StateScope(SqlServerLogger logger, object state)
            {
                _logger = logger;
                State = state;
            }

            public void Dispose()
            {
                _logger._scopes.Pop();
            }
        }
    }
}
