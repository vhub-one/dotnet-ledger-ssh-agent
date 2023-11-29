using Common.Hosting.Configuration;
using LedgerSshAgentService.Commands.SshAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                #region [LedgerSshAgent]

                services.ConfigureByName<LedgerSshAgentOptions>();
                services.AddSingleton<ISshAgent, LedgerSshAgent>();

                #endregion

                #region [SshAgentService]

                services.ConfigureByName<SshAgentPipeOptions>();
                services.AddSingleton<ISshAgentConnectionFactory, SshAgentPipeConnectionFactory>();
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