using Ssh;

namespace LedgerSshAgentService.Commands.List
{
    public class ListCommandOptions
    {
        public string[] KeyPaths { get; set; }
        public SshKeyCurve KeyCurve { get; set; }
        public string DeviceId { get; set; }
    }
}