using LedgerSshAgentService.Commands.List;
using Microsoft.Extensions.DependencyInjection;
using Ssh;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace LedgerSshAgentService
{
    internal partial class ServiceBootstrap
    {
        static void InitListCommand(Command command)
        {
            var listKeyPathArgument = new Argument<string[]>("paths")
            {
                Description = "Key paths list",
                Arity = ArgumentArity.OneOrMore
            };
            var listKeyCurveOption = new Option<SshKeyCurve>("--curve")
            {
                Description = "Key curve",
                Arity = ArgumentArity.ExactlyOne
            };
            var listDeviceIdOption = new Option<string>("--device")
            {
                Description = "Device id to use",
                Arity = ArgumentArity.ZeroOrOne
            };

            listKeyCurveOption.SetDefaultValue(
                SshKeyCurve.P256V1
            );

            var listCommand = new Command("list")
            {
                Description = "Get public keys by provided path"
            };

            listCommand.AddArgument(listKeyPathArgument);
            listCommand.AddOption(listKeyCurveOption);
            listCommand.AddOption(listDeviceIdOption);
            listCommand.SetHandler(
                context => HandleListCommandAsync(context, listKeyPathArgument, listKeyCurveOption, listDeviceIdOption)
            );

            command.AddCommand(listCommand);
        }

        static async Task HandleListCommandAsync(InvocationContext context, Argument<string[]> keyPaths, Option<SshKeyCurve> keyCurve, Option<string> deviceId)
        {
            await HandleCommandAsync(context, (hostBuilder) => {

                hostBuilder.ConfigureServices((hostContext, services) => {

                    services.AddSingleton(context.Console);

                    #region [ListCommandBackgroundService]

                    services.Configure<ListCommandOptions>(
                        options =>
                        {
                            options.KeyPaths = context.ParseResult.GetValueForArgument(keyPaths);
                            options.KeyCurve = context.ParseResult.GetValueForOption(keyCurve);
                            options.DeviceId = context.ParseResult.GetValueForOption(deviceId);
                        }
                    );
                    services.AddHostedService<ListCommandBackgroundService>();

                    #endregion
                });
            });
        }
    }
}