using Common.Hosting.Configuration;
using Common.Hosting.DependencyInjection;
using Ledger;
using Ledger.Transport.Speculos;
using Ledger.Transport.Usb;
using Ledger.Transport.WinBle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;

namespace LedgerSshAgentService
{
    internal partial class ServiceBootstrap
    {
        static Task<int> Main(params string[] args)
        {
            var command = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = true
            };

            InitSshAgentCommand(command);
            InitListCommand(command);

            return command.InvokeAsync(args);
        }

        static async Task HandleCommandAsync(InvocationContext commandContext, Action<HostBuilder> configureCommandHost)
        {
            try
            {
                var hostBuilder = new HostBuilder();

                ConfigureHost(hostBuilder);
                configureCommandHost(hostBuilder);

                var host = hostBuilder.Build();
                var hostStoppingToken = commandContext.GetCancellationToken();

                // Start generic host
                await host.RunAsync(
                    hostStoppingToken
                );
            }
            catch (Exception ex)
            {
                commandContext.Console.Error.WriteLine(ex.Message);
                commandContext.Console.Error.WriteLine(ex.StackTrace);
            }
        }

        static void ConfigureHost(HostBuilder hostBuilder)
        {
            hostBuilder.ConfigureHostConfiguration(builder =>
            {
                // File configuration
                builder.AddJsonFile("config.json", true);
            });

            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    // Load configuration from logging section
                    builder.AddConfiguration(context.Configuration.GetSection("Logging"));

                    // Register loggers
                    builder.AddConsole();
                    builder.AddEventLog();
                });

                // Configure common services
                ConfigureCommonServices(services);
            });
        }

        static void ConfigureCommonServices(IServiceCollection services)
        {
            #region [LedgerDeviceEnumerator]

            // SPECULOS
            services.ConfigureByName<LedgerSpeculosOptions>();

            services.AddSingleton<ILedgerDeviceEnumerator>(p =>
                new LedgerAggregatedDeviceEnumerator(
                    p.CreateService<LedgerUsbDeviceEnumerator>(),
                    p.CreateService<LedgerBleDeviceEnumerator>(),
                    p.CreateService<LedgerSpeculosDeviceEnumerator>()
                )
            );

            #endregion
        }
    }
}