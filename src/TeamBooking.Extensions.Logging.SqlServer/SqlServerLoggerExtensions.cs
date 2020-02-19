using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using TeamBooking.Extesions.Logging.SqlServer;

namespace Microsoft.Extensions.Logging
{
    public static class SqlServerLoggerExtensions
    {
        public static ILoggingBuilder AddSqlServer(this ILoggingBuilder builder, Action<SqlServerLoggerOptions> configure)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SqlServerLoggerProvider>());
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
