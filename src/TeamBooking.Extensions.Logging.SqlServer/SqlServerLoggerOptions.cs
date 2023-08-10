using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace TeamBooking.Extensions.Logging.SqlServer
{
    public class SqlServerLoggerOptions
    {
        public Func<string?, string>? GetConnectionString { get; set; }
        public TimeSpan BatchInterval { get; set; } = TimeSpan.FromSeconds(10);
        public string TableName { get; set; } = null!;
        public string LoggerColumnName { get; set; } = null!;
        public string MessageColumnName { get; set; } = null!;
        public string LogLevelColumnName { get; set; } = null!;
        public List<MetaMapping> MetaMappings { get; set; } = new List<MetaMapping>();
        public Func<LogLevel, object> MapLogLevel { get; set; } = x => (int)x;
    }

    public class MetaMapping
    {
        public string ColumnName { get; set; }
        public object DefaultValue { get; set; }
        public string[] LogTemplateKeys { get; set; }

        public MetaMapping(string columnName, object defaultValue, string[]? logTemplateKeys = null)
        {
            ColumnName = columnName;
            DefaultValue = defaultValue;
            LogTemplateKeys = logTemplateKeys ?? new[] { columnName };
        }
    }
}
