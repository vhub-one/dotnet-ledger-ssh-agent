using Common.Hosting.Configuration;
using Common.Hosting.DependencyInjection;
using LedgerSshAgentService.Commands.SshAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SshAgent;
using SshAgent.Ledger;
using SshAgent.Transport.Pipe;
using System.CommandLine;

namespace LedgerSshAgentService
{
    internal partial class ServiceBootstrap
    {
        static void InitSshAgentCommand(Command command)
        {
            command.Description = "Intercepts sign requests to ssh agent and uses Ledger device to handle them";

            command.SetHandler(context => HandleCommandAsync(context, ConfigureSshAgentHost));
        }

        static void ConfigureSshAgentHost(HostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((context, services) =>
            {
                #region [SshAgent]

                services.ConfigureByName<LedgerSshAgentOptions>();
                services.ConfigureByName<PipeSshAgentOptions>();

                services.AddSingleton<ISshAgent>(p =>
                    new SshAgentAggregator(
                        p.GetRequiredService<ILogger<SshAgentAggregator>>(),
                        p.CreateService<LedgerSshAgent>(),
                        p.CreateService<PipeSshAgent>()
                    )
                );

                #endregion

                #region [SshAgentService]

                services.ConfigureByName<PipeSshAgentConnectionFactoryOptions>();
                services.AddSingleton<ISshAgentConnectionFactory, PipeSshAgentConnectionFactory>();
                services.AddSingleton<SshAgentService>();

                #endregion

                #region [SshAgentBackgroundService]

                services.AddHostedService<SshAgentBackgroundService>();

                #endregion
            });

            hostBuilder.UseWindowsService();
        }
    }
}