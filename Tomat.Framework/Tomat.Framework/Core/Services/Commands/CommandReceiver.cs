#region License
// Copyright (C) 2021 Tomat and Contributors, MIT License
#endregion

using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Tomat.Framework.Common.Embeds;
using Tomat.Framework.Core.Bot;
using Tomat.Framework.Core.CommandContext;
using Tomat.Framework.Core.Utilities;

namespace Tomat.Framework.Core.Services.Commands
{
    public class CommandReceiver : IInitializableService
    {
        public IServiceProvider ServiceProvider { get; }

        public DiscordShardedClient Client { get; }

        public DiscordBot Bot { get; }

        public CommandService Commands { get; protected set; }

        public CommandReceiver(IServiceProvider serviceProvider, DiscordShardedClient client, DiscordBot bot)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Bot = bot;
            Commands = new CommandService();
        }

        public async Task InitializeAsync()
        {
            Commands = ServiceProvider.GetRequiredService<CommandService>();

            Client.MessageReceived += ReceiveCommand;
            Commands.CommandExecuted += HandleErrors;

            foreach (Assembly assembly in Bot.Assemblies)
                await Commands.AddModulesAsync(assembly, ServiceProvider);

            await Task.CompletedTask;
        }

        private async Task ReceiveCommand(SocketMessage message)
        {
            if (message.Channel is not SocketTextChannel tChannel)
                return;

            DiscordSocketClient shard = Client.GetShardFor(tChannel.Guild);

            if (message.ValidateMessageMention(
                Bot,
                out CommandUtilities.InvalidMessageReason invalidReason,
                out int argPos,
                shard
            ))
            {
                if (message is not SocketUserMessage socketMessage)
                {
                    ISocketMessageChannel channel = message.Channel;

                    EmbedBuilder embed = Bot.CreateSmallEmbed(
                        message.Author,
                        $"Message resolve failed with result: {invalidReason}" +
                        "\nIf you feel this is a mistake, please report it to the developers!"
                    ).WithTitle("Uncaught: Invalid message received?");

                    await channel.SendMessageAsync(embed: embed.Build());
                    return;
                }

                await Commands.ExecuteAsync(new BotCommandContext(Bot, shard, socketMessage), argPos, null);
            }
        }

        private async Task HandleErrors(Optional<CommandInfo> info, ICommandContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                BaseEmbed embed = new(Client.CurrentUser, context.User)
                {
                    Title = $"Error encountered: {result.Error}",
                    Description = result.ErrorReason
                };

                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }
    }
}