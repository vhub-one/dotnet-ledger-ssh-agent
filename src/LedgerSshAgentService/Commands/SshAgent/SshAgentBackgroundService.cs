using Microsoft.Extensions.Hosting;
using SshAgent;

namespace LedgerSshAgentService.Commands.SshAgent
{
    public class SshAgentBackgroundService : BackgroundService
    {
        private readonly SshAgentService _sshAgentProxy;

        public SshAgentBackgroundService(SshAgentService sshAgentProxy)
        {
            _sshAgentProxy = sshAgentProxy;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await _sshAgentProxy.RunAsync(token);
        }
    }
}