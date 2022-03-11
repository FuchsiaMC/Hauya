using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Hauya.Common;
using Hauya.Content.Handlers;
using MongoDB.Bson;
using MongoDB.Driver;
using Tomat.Framework.Common.Embeds;
using Tomat.Framework.Core.CommandContext;
using Tomat.Framework.Core.Services.Commands;

namespace Hauya.Content.Commands.Systems
{
    [ModuleInfo("Administration")]
    public sealed class AdministrationModule : ModuleBase<BotCommandContext>
    {
        [Command("participation")]
        [Summary("Manages the participation system for the Discord bot.")]
        [Parameters("<subcommand>")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ParticipationCommand([Remainder] string command)
        {
            BsonDocument? config = (Context.Bot as HauyaBot)?.Configuration;

            if (config == null)
                throw new Exception("Config failed to load in participation command.");

            if (Context.User is not SocketGuildUser guildUser)
                throw new Exception("SocketUser cast to SocketGuildUser failed in participation command.");

            BsonDocument participationConfig = config.GetElement("systems").Value.AsBsonDocument
                .GetElement("participation").Value.AsBsonDocument;
            
            string[] args = command.Split(" ");

            switch (args[0])
            {
                case "setup":
                    HauyaEmbedBuilder embed = new HauyaEmbedBuilder()
                        .WithRoleColor(Context.Guild.GetUser(Context.Bot.User.Id))
                        .WithDatabaseDescription(participationConfig)
                        .WithDatabaseFooter(participationConfig, Context.Guild)
                        .WithCurrentTimestamp();
                    
                    ComponentBuilder components = new();
                    
                    foreach (BsonValue buttonValue in participationConfig
                        .GetElement("buttons").Value.AsBsonArray.Values)
                    {
                        BsonDocument buttonDoc = buttonValue.AsBsonDocument;
                        
                        components.WithButton(
                            buttonDoc.GetElement("label").Value.AsString,
                            buttonDoc.GetElement("id").Value.AsString,
                            emote: new Emoji(buttonDoc.GetElement("emote").Value.AsString
                            ));
                    }

                    await Context.Guild.GetTextChannel((ulong) participationConfig
                        .GetElement("channel_id").Value.AsInt64
                    ).SendMessageAsync(embed: embed.Build(), components: components.Build());
                    
                    HauyaEmbedBuilder counterEmbed = new HauyaEmbedBuilder()
                        .WithRoleColor(Context.Guild.GetUser(Context.Bot.User.Id))
                        .WithInformation("Participation Counter", "This value shows the amount of participants in the next event compared to the goal." +
                                                                  "\n" +
                                                                  "\n**[" + (Context.Bot as HauyaBot)!.Participation.GetSubmittedSubmissions().Count() + "/20]**")
                        .WithDatabaseCommonFooter(config,"fuchsia_minecraft", Context.Guild)
                        .WithCurrentTimestamp();
                    
                    RestUserMessage msg = await Context.Guild.GetTextChannel((ulong) participationConfig
                        .GetElement("channel_id").Value.AsInt64
                    ).SendMessageAsync(embed: counterEmbed.Build());
                    
                   // BsonDocument newParticipation = participationConfig.Add("count_message_id", (long)msg.Id).;
                    
                    await (Context.Bot as HauyaBot)!.Database.GetCollection<BsonDocument>("config").UpdateOneAsync(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter
                                .Eq("name", Debugger.IsAttached ? "development" : "production")),
                        Builders<BsonDocument>.Update.Set("count_message_id", msg.Id)
                    );
                    
                    break;

                case "fixroles":
                    HauyaBot bot = (Context.Bot as HauyaBot)!;
                    foreach (BsonDocument doc in bot.Participation.GetSubmittedSubmissions())
                    {
                        long id = doc.GetElement("discord_id").Value.AsInt64;

                        await Context.Guild.GetUser((ulong)id).AddRoleAsync(938690755189432331);
                    }
                    
                    break;
            }
        }

        [Command("config")]
        [Summary("Manages the config system for the Discord bot.")]
        [Parameters("<subcommand>")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ConfigCommand([Remainder] string command)
        {
            string[] args = command.Split(" ");
            
            if (args[0] == "reload")
            {
                if (Context.Bot is not HauyaBot bot)
                    throw new Exception("Failed to load bot in config command.");

                bot.Configuration = HauyaBot.GetConfig(bot.Database).Result;
                bot.DiscordClient.ButtonExecuted -= bot.Participation.ParticipationButtonHandler;
                //bot.Participation = new ParticipationHandler(bot, bot.Database);
                //bot.DiscordClient.ButtonExecuted += bot.Participation.ParticipationButtonHandler;
                bot.Participation.SetupConfiguration();

                // todo: make fancy embed later
                await Context.Channel.SendMessageAsync("Successfully reloaded my config.");
            }
        }
        
        [Command("restart")]
        [Summary("Restarts the Discord bot")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RestartCommand()
        {
            if (Context.Bot is not HauyaBot bot)
                throw new Exception("Failed to load bot in restart command.");

            // todo: run script ig
        }
    }
}