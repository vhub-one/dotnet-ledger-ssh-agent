using Common.Hosting.Configuration;
using Common.Hosting.Service;
using Common.Hosting.ServiceProvider;
using LedgerSshAgentService.Commands.SshAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SshAgent;
using SshAgent.Ledger;
using SshAgent.Proxy;
using SshAgent.Transport.Pipe;
using SshAgent.Transport.TcpSocket;
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

                services.ConfigureByName<PipeSshAgentClientOptions>();
                services.ConfigureByName<LedgerSshAgentClientOptions>();
                services.ConfigureByName<SocketSshAgentClientOptions>();

                services.AddSingleton(p =>
                {
                    var providers = new Dictionary<string, ISshAgent>
                    {
                        { "PipeSshAgentClient", p.CreateService<PipeSshAgentClient>() },
                        { "LedgerSshAgentClient", p.CreateService<LedgerSshAgentClient>() },
                        { "SocketSshAgentClient", p.CreateService<SocketSshAgentClient>() },
                    };

                    return ServiceMap.Create(providers);
                });

                services.ConfigureByName<SshAgentProxyOptions>();
                services.AddSingleton<ISshAgent, SshAgentProxy>();

                #endregion

                #region [SshAgentService]

                services.ConfigureByName<PipeSshAgentHostConnectionFactoryOptions>();
                services.ConfigureByName<SocketSshAgentHostConnectionFactoryOptions>();

                services.AddSingleton(p => {

                    var factories = new Dictionary<string, ISshAgentHostConnectionFactory>
                    {
                        { "PipeSshAgentHostConnectionFactory", p.CreateService<PipeSshAgentHostConnectionFactory>() },
                        { "SocketSshAgentHostConnectionFactory", p.CreateService<SocketSshAgentHostConnectionFactory>() },
                    };

                    return ServiceMap.Create(factories);
                });

                services.ConfigureByName<SshAgentConnectionFactoryProxyOptions>();
                services.AddSingleton<ISshAgentHostConnectionFactory, SshAgentConnectionFactoryProxy>();

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