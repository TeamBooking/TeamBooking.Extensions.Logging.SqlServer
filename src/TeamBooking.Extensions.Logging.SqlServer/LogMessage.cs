using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TeamBooking.Extesions.Logging.SqlServer
{
    internal readonly struct LogMessage
    {
        public int SystemId { get; }
        public string Logger { get; }
        public string FormattedMessage { get; }
        public LogLevel LogLevel { get; }
        public List<object> MetadataValues { get; }

        public LogMessage(int systemId, string logger, string formattedMessage, LogLevel logLevel, List<object> metadataValues)
        {
            SystemId = systemId;
            Logger = logger;
            FormattedMessage = formattedMessage;
            LogLevel = logLevel;
            MetadataValues = metadataValues;
        }
    }
}
