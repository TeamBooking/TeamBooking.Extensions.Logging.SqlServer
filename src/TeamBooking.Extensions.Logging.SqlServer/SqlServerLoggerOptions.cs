using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace TeamBooking.Extesions.Logging.SqlServer
{
    public class SqlServerLoggerOptions
    {
        public Func<int, string> GetConnectionString { get; set; }
        public TimeSpan BatchInterval { get; set; } = TimeSpan.FromSeconds(10);
        public string TableName { get; set; }
        public string LoggerColumnName { get; set; }
        public string MessageColumnName { get; set; }
        public string LogLevelColumnName { get; set; }
        public List<MetaMapping> MetaMappings { get; set; } = new List<MetaMapping>();
        public Func<LogLevel, object> MapLogLevel { get; set; } = x => (int)x;
    }

    public class MetaMapping
    {
        public string LogTemplateKey { get; set; }
        public string ColumnName { get; set; }
        public object DefaultValue { get; set; }

        private MetaMapping()
        {
        }

        public static MetaMapping Null(string key, string columnName = null)
        {
            return new MetaMapping()
            {
                LogTemplateKey = key,
                ColumnName = columnName ?? key
            };
        }

        public static MetaMapping NotNull(string key, object defaultValue) => NotNull(key, key, defaultValue);

        public static MetaMapping NotNull(string key, string columnName, object defaultValue)
        {
            return new MetaMapping()
            {
                LogTemplateKey = key,
                ColumnName = columnName,
                DefaultValue = defaultValue
            };
        }
    }
}
