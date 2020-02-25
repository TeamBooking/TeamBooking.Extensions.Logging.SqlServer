using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using TeamBooking.Extesions.Logging.SqlServer;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddLogging(logging => logging
                    .AddConsole()
                    .AddSqlServer(options =>
                    {
                        options.GetConnectionString = systemId => "Server=localhost;Database=Tb5;Trusted_Connection=True;MultipleActiveResultSets=True";
                        options.BatchInterval = TimeSpan.FromMilliseconds(100);
                        options.TableName = "Hist";
                        options.LoggerColumnName = "ObjeNavn";
                        options.MessageColumnName = "LogTeks";
                        options.LogLevelColumnName = "HistLevel";
                        options.MetaMappings.Add(new MetaMapping("PostId", null, new[] { "PostId", "PackageId" }));
                        options.MetaMappings.Add(new MetaMapping("HistFiltId", 0));
                    }))
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Hello post {PostId}", 123);
            logger.LogInformation("Hello package {PackageId}", 123);

            using (var scope = logger.BeginScope("User {UserId}", 1))
            {
                logger.LogError("Hello error");
            }

            Console.WriteLine("Press a key to exit");
            Console.ReadLine();
        }
    }
}
