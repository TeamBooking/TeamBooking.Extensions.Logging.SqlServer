using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TeamBooking.Extensions.Logging.SqlServer
{
    internal readonly record struct LogMessage
    {
        public string? Tenant { get; }
        public string Logger { get; }
        public string FormattedMessage { get; }
        public LogLevel LogLevel { get; }
        public List<object> MetadataValues { get; }

        public LogMessage(
            string? tenant,
            string logger,
            string formattedMessage,
            LogLevel logLevel,
            List<object> metadataValues
        )
        {
            Tenant = tenant;
            Logger = logger;
            FormattedMessage = formattedMessage;
            LogLevel = logLevel;
            MetadataValues = metadataValues;
        }
    }
}
