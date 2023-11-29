using Ledger;
using Ledger.App.SshPgp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssh;
using System.Buffers;
using System.CommandLine;

namespace LedgerSshAgentService.Commands.List
{
    public class ListCommandBackgroundService : BackgroundService
    {
        private readonly IOptions<ListCommandOptions> _optionsAccessor;
        private readonly ILedgerDeviceEnumerator _deviceEnumerator;
        private readonly IConsole _console;
        private readonly IHostApplicationLifetime _lifetime;

        public ListCommandBackgroundService(IOptions<ListCommandOptions> optionsAccessor, ILedgerDeviceEnumerator deviceEnumerator, IConsole console, IHostApplicationLifetime lifetime)
        {
            _optionsAccessor = optionsAccessor;
            _deviceEnumerator = deviceEnumerator;
            _console = console;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                await HandleCommandAsync(token);
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }

        private async Task HandleCommandAsync(CancellationToken token)
        {
            var options = _optionsAccessor.Value;

            if (options == null)
            {
                throw new InvalidOperationException("Configuration is missing");
            }

            var ledgerDevices = _deviceEnumerator.GetDevicesAsync(token);

            await foreach (var device in ledgerDevices)
            {
                if (options.DeviceId != null &&
                    options.DeviceId != device.Id)
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
                        _console.WriteLine(string.Format("Device is not available [{0}]", device.Id));
                    }
                    else
                    {
                        _console.WriteLine(ex.Message);
                        _console.WriteLine(ex.StackTrace);
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

                    foreach (var keyPath in options.KeyPaths)
                    {
                        _console.WriteLine(keyPath);

                        try
                        {
                            await PrintPublicKeyAsync(client, keyPath, options.KeyCurve, token);
                        }
                        catch (LedgerSshPgpAppStoppedException)
                        {
                            _console.WriteLine("Ledger device is not ready");

                            // Stop iteratin keys
                            break;
                        }
                        catch (LedgerSshPgpAppCancelledException)
                        {
                            _console.WriteLine("Operation cancelled by user");

                            // Go to the next key
                            continue;
                        }
                        catch (LedgerDeviceException)
                        {
                            _console.WriteLine("Ledger device is not available");

                            // Stop iteratin keys
                            break;
                        }
                    }
                }

                break;
            }
        }

        private async ValueTask PrintPublicKeyAsync(LedgerSshPgpAppClient client, string keyPath, SshKeyCurve keyCurve, CancellationToken token)
        {
            var sshKeyWriter = new ArrayBufferWriter<byte>();

            // Get public key from ledger device
            var sshKey = await client.GetPublicKeyAsync(
                keyPath,
                keyCurve,
                token
            );

            if (sshKey != null)
            {
                sshKey.WriteTo(sshKeyWriter);
            }

            var keyBlob64 = Convert.ToBase64String(sshKeyWriter.WrittenSpan);

            if (keyBlob64.Length > 0)
            {
                _console.WriteLine($"{sshKey.Algorithm} {keyBlob64}");
            }
            else
            {
                _console.WriteLine($"Key is not supported");
            }
        }
    }
}