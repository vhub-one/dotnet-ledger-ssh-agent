using Ssh;

namespace SshAgent.Ledger.Contracts
{
    public class KeyIdentity
    {
        public string KeyPath { get; set; }
        public string Key { get; set; }
        public SshKeyCurve KeyCurve { get; set; }
        public string LedgerId { get; set; }
    }
}
