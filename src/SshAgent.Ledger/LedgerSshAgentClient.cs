using Ledger;
using Ledger.App.SshPgp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssh;
using SshAgent.Contract;
using SshAgent.Ledger.Contracts;
using System.Buffers;

namespace SshAgent.Ledger
{
    public class LedgerSshAgentClient : ISshAgent
    {
        private readonly IOptions<LedgerSshAgentClientOptions> _ledgerOptionsAccessor;
        private readonly ILedgerDeviceEnumerator _ledgerEnumerator;

        private readonly ILogger<LedgerSshAgentClient> _logger;

        public LedgerSshAgentClient(IOptions<LedgerSshAgentClientOptions> ledgerOptionsAccessor, ILedgerDeviceEnumerator ledgerEnumerator, ILogger<LedgerSshAgentClient> logger)
        {
            _ledgerOptionsAccessor = ledgerOptionsAccessor;
            _ledgerEnumerator = ledgerEnumerator;

            _logger = logger;
        }

        public ValueTask<IdentitiesReply> RequestIdentitiesAsync(CancellationToken token)
        {
            var ledgerOptions = _ledgerOptionsAccessor.Value;

            if (ledgerOptions == null ||
                ledgerOptions.Keys == null)
            {
                throw new InvalidOperationException("Configuration for LedgerSshAgent is missing");
            }

            var keys = new List<IdentityKey>();
            var keysReply = new IdentitiesReply
            {
                Keys = keys
            };

            foreach (var keyIdentity in ledgerOptions.Keys)
            {
                var keyBlob64 = keyIdentity.Key;
                var keyBlob = Convert.FromBase64String(keyBlob64);

                if (keyBlob == null)
                {
                    throw new InvalidOperationException("Configuration for LedgerSshKey is missing");
                }

                var identityKey = new IdentityKey
                {
                    Comment = keyIdentity.KeyPath,
                    KeyBlob = keyBlob
                };

                keys.Add(identityKey);
            }

            return ValueTask.FromResult(keysReply);
        }

        public async ValueTask<SignReply> SignAsync(SignRequest signRequest, CancellationToken token)
        {
            var key = SearchForKey(signRequest.KeyBlob.Span);

            if (key == null)
            {
                _logger.LogError("Requested key doesn't exist");

                throw new SshAgentNotAvailableException();
            }

            var ledgerDevices = _ledgerEnumerator.GetDevicesAsync(token);

            await foreach (var device in ledgerDevices)
            {
                if (key.LedgerId != null &&
                    key.LedgerId != device.Id)
                {
                    // Skip device
                    continue;
                }

                var channel = default(ILedgerDeviceChannel);

                try
                {
                    // Try to open channel
                    channel = await device.OpenChannelAsync(token);
                }
                catch (Exception ex)
                {
                    if (ex is LedgerDeviceNotAvailableException)
                    {
                        _logger.LogInformation("Device is not available [{device}]", device.Id);
                    }
                    else
                    {
                        _logger.LogError(ex, "Unable to open channel to device [{device}]", device.Id);
                    }
                }

                if (channel == null)
                {
                    // Try another device
                    continue;
                }

                await using (channel)
                {
                    var client = new LedgerSshPgpAppClient(channel);
                    var clientSignResult = await SignWithKeyAsync(client, key, signRequest, token);

                    return clientSignResult;
                }
            }

            _logger.LogError("There are no ledger devices to sign request");

            throw new SshAgentNotAvailableException();
        }

        private async ValueTask<SignReply> SignWithKeyAsync(LedgerSshPgpAppClient client, KeyIdentity keyIdentity, SignRequest signRequest, CancellationToken token)
        {
            SshSignature signature;

            try
            {
                // Use first available ledger device
                signature = await client.SignDataAsync(
                    keyIdentity.KeyPath,
                    keyIdentity.KeyCurve,
                    signRequest.DataBlob,
                    token
                );
            }
            catch (LedgerSshPgpAppCancelledException ex)
            {
                throw new SshAgentCancelledException(ex);
            }
            catch (LedgerSshPgpAppStoppedException ex)
            {
                throw new SshAgentNotReadyException(ex);
            }
            catch (LedgerDeviceException ex)
            {
                throw new SshAgentNotAvailableException(ex);
            }

            var signatureWriter = new ArrayBufferWriter<byte>();

            if (signature != null)
            {
                signature.WriteTo(signatureWriter);
            }

            return new SignReply
            {
                SignatureBlob = signatureWriter.WrittenMemory
            };
        }

        private KeyIdentity SearchForKey(ReadOnlySpan<byte> keyBlob)
        {
            var ledgerOptions = _ledgerOptionsAccessor.Value;

            if (ledgerOptions == null ||
                ledgerOptions.Keys == null)
            {
                throw new InvalidOperationException("Configuration for LedgerSshAgent is missing");
            }

            foreach (var identity in ledgerOptions.Keys)
            {
                var identityKey = identity.Key;
                var identityKeyBlob = Convert.FromBase64String(identityKey);

                if (keyBlob.SequenceEqual(identityKeyBlob))
                {
                    return identity;
                }
            }

            return null;
        }
    }
}