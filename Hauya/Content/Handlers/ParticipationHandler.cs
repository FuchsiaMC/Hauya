using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Hauya.Common;
using Hauya.Utilities;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tomat.Framework.Core.Utilities;

namespace Hauya.Content.Handlers
{
    // participants
    // submissions
    // ongoing submission 
    // none, ongoing, submitted, cancelled
    // submitter
    public class ParticipationHandler
    {
        private HauyaBot Bot { get; }
        
        private IEnumerable<BsonValue> ButtonResponses { get; set; } = null!;
        private BsonDocument RequestInformation { get; set; } = null!;

        private IMongoCollection<BsonDocument> Collection { get; set; } = null!;
        private int Counter { get; set; }
        
        public ParticipationHandler(HauyaBot hauyaBot, IMongoDatabase database)
        {
            Bot = hauyaBot;

            SetupConfiguration();
            SetupParticipants(database);
        }

        public void SetupConfiguration()
        {
            BsonDocument participationConfig = Bot.Configuration
                .GetElement("systems").Value.AsBsonDocument
                .GetElement("participation").Value.AsBsonDocument;
            
            ButtonResponses = participationConfig
                .GetElement("responses").Value.AsBsonArray
                .Values;

            RequestInformation = participationConfig
                .GetElement("request").Value.AsBsonDocument;
        }

        public void SetupParticipants(IMongoDatabase database)
        {
            Collection = database.GetCollection<BsonDocument>("participants");
            Counter = Collection.FindAsync(Builders<BsonDocument>.Filter.Empty).Result.ToEnumerable().Count();
        }

        public async Task ParticipationButtonHandler(SocketMessageComponent component)
        {
            if (component.Message.Author is not SocketGuildUser botUser)
                throw new Exception("Guild user cast exception in participation button handler.");

            switch (component.Data.CustomId)
            {
                case "participation":
                    // if they already have an ongoing submission
                    if (HasOngoingSubmission(component.User, out BsonDocument? ongoingSubmission)) {
                        await HandleWaitingStatus(component, botUser, ongoingSubmission!);
                        return;
                    }
                    
                    // progress the counter and create the channel
                    Counter += 1;
                    RestTextChannel channel = await CreateSubmissionChannel(component, botUser);

                    // add the submission to the database
                    await InsertSubmission(component.User, channel);
    
                    // set up the channel with the topic and first question
                    await SetupChannel(botUser, component.User, channel);
                    
                    // respond with the channel opened response
                    BsonDocument channelOpenedResponse = ButtonResponses.First(e => e.AsBsonDocument
                        .GetElement("id").Value.AsString == "channel_opened").AsBsonDocument;

                    // build the embed
                    HauyaEmbedBuilder embedBuilder = new HauyaEmbedBuilder()
                        .WithRoleColor(botUser)
                        .WithDatabaseTitle(channelOpenedResponse)
                        .WithFormattedDatabaseDescription(channelOpenedResponse, channel.Mention)
                        .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", botUser.Guild)
                        .WithCurrentTimestamp();

                    // send the response
                    await component.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
                    break;
                case "cancel_submission":
                    BsonDocument toBeCancelledSubmission = GetOngoingSubmission(component.User) ?? throw new Exception("the fuck its null bitch?");

                    await Collection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("discord_id", component.User.Id),
                            Builders<BsonDocument>.Filter.Eq("status", Status.Ongoing.ToString())),
                        Builders<BsonDocument>.Update.Set("status", Status.Cancelled.ToString())
                        );
                    
                    SocketTextChannel toBeDeletedChannel = botUser.Guild
                        .GetTextChannel((ulong) toBeCancelledSubmission.GetElement("channel_id").Value.AsInt64);

                    await toBeDeletedChannel.DeleteAsync();
                    break;
            }
        }

        private async Task<RestTextChannel> CreateSubmissionChannel(SocketInteraction component, SocketGuildUser botUser)
        {
            RestTextChannel channel =
                await botUser.Guild.CreateTextChannelAsync("participant-" + Counter,
                    options =>
                    {
                        options.CategoryId = (ulong) Bot.Configuration
                            .GetElement("systems").Value.AsBsonDocument
                            .GetElement("participation").Value.AsBsonDocument
                            .GetElement("category_id").Value.AsInt64;
                    });

            // hide from everyone
            OverwritePermissions everyonePerms = new(viewChannel: PermValue.Deny);
            await channel.AddPermissionOverwriteAsync(botUser.Guild.EveryoneRole, everyonePerms);
            
            // give perms to the submitter
            OverwritePermissions userPerms = new(
                viewChannel: PermValue.Allow,
                readMessageHistory: PermValue.Allow,
                sendMessages: PermValue.Deny
            );

            await channel.AddPermissionOverwriteAsync(component.User, userPerms);
            return channel;
        }

        private IEnumerable<BsonDocument> GetSubmissions(IUser user)
        {
            return Collection
                .FindAsync(Builders<BsonDocument>.Filter.Eq("discord_id", user.Id)).Result
                .ToEnumerable();
        }

        private bool HasOngoingSubmission(IUser user)
        {
            return GetOngoingSubmission(user) != null;
        }
        
        private bool HasOngoingSubmission(IUser user, out BsonDocument? document)
        {
            document = GetOngoingSubmission(user);
            return document != null;
        }

        private BsonDocument? GetOngoingSubmission(IUser user)
        {
            return GetSubmissions(user).FirstOrDefault(submission => submission
                .GetElement("status").Value.AsString == Status.Ongoing.ToString());
        }
        
        private async Task InsertSubmission(IUser user, IGuildChannel channel)
        {
            await Collection.InsertOneAsync(new BsonDocument
            {
                { "status", Status.Ongoing.ToString() },
                { "timestamp", (long) DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds },
                { "discord_id", (long) user.Id }, 
                { "discord_username", user.Username + "#" + user.Discriminator },
                { "channel_id", (long) channel.Id }
            });
        }

        private async Task HandleWaitingStatus(IDiscordInteraction component, SocketGuildUser botUser,
            BsonDocument submission)
        {
            // get the channel from the submission
            SocketTextChannel channel = botUser.Guild
                .GetTextChannel((ulong) submission.GetElement("channel_id").Value.AsInt64);

            // get the "channel existing" button response
            BsonDocument buttonResponse = ButtonResponses.First(e => e.AsBsonDocument
                .GetElement("id").Value.AsString == "channel_existing").AsBsonDocument;

            // build the embed with our data
            HauyaEmbedBuilder embed = new HauyaEmbedBuilder()
                .WithRoleColor(botUser)
                .WithDatabaseTitle(buttonResponse)
                .WithFormattedDatabaseDescription(buttonResponse, channel.Mention)
                .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", botUser.Guild)
                .WithCurrentTimestamp();

            // send the response
            await component.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        private readonly List<ulong> questionAnsweredUsers = new();
        
        private async Task SetupChannel(SocketGuildUser botUser, IUser user, RestTextChannel channel)
        {
            // build the request information embed
            HauyaEmbedBuilder embed = new HauyaEmbedBuilder()
                .WithRoleColor(botUser)
                .WithDatabaseInformation(RequestInformation)
                .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", botUser.Guild)
                .WithCurrentTimestamp();

            ComponentBuilder components = new ComponentBuilder()
                .WithButton("Cancel", "cancel_submission", ButtonStyle.Danger, new Emoji("🛑"));
            
            // send it
            await channel.SendMessageAsync(embed: embed.Build(), components: components.Build());

            // get all the questions
            IEnumerable<BsonDocument> questions =
                RequestInformation.GetElement("questions").Value.AsBsonArray.Values.Select(value => value.AsBsonDocument);

            async void Start()
            {
                try
                {
                    Thread.CurrentThread.IsBackground = true;

                    foreach (BsonDocument question in questions)
                    {
                        HauyaEmbedBuilder questionEmbed = new HauyaEmbedBuilder()
                            .WithRoleColor(botUser)
                            .WithDatabaseInformation(question)
                            .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", botUser.Guild)
                            .WithCurrentTimestamp();

                        ComponentBuilder componentBuilder = new();

                        if (question.GetElement("type").Value.AsString == "text")
                        {
                            OverwritePermissions userPerms = new(viewChannel: PermValue.Allow,
                                readMessageHistory: PermValue.Allow, sendMessages: PermValue.Allow);

                            await channel.AddPermissionOverwriteAsync(user, userPerms);
                        }
                        else if (question.GetElement("type").Value.AsString == "selection")
                        {
                            OverwritePermissions userPerms = new(viewChannel: PermValue.Allow,
                                readMessageHistory: PermValue.Allow, sendMessages: PermValue.Deny);

                            await channel.AddPermissionOverwriteAsync(user, userPerms);

                            if (question.GetElement("id").Value.AsString == "timezone")
                            {
                                int index = 0;
                                foreach (List<TimeZoneInfo> timezoneSplit in TimeZoneInfo.GetSystemTimeZones()
                                    .Where(t => t.Id.StartsWith("Australia")).ToList().Split(25))
                                {
                                    if (index > 4)
                                    {
                                        Console.WriteLine("out of bounds: too many countries");
                                        break;
                                    }
                                    
                                    SelectMenuBuilder timezoneSelection = new SelectMenuBuilder()
                                        .WithPlaceholder("Select a timezone!")
                                        .WithMaxValues(1)
                                        .WithMinValues(1)
                                        .WithCustomId("timezone"); // can break shit

                                    foreach (TimeZoneInfo timezone in timezoneSplit)
                                    {
                                        timezoneSelection.AddOption(
                                            timezone.Id,
                                            //timezone.Id.Replace("/", "-").ToLower(),
                                            timezone.Id,
                                            timezone.DisplayName,
                                            new Emoji("🗺️")
                                        );
                                    }

                                    if (timezoneSplit.Count < 25)
                                    {
                                        timezoneSelection.AddOption("Other", "other", 
                                            "Anything not shown above", new Emoji("📜"));
                                    }

                                    componentBuilder.WithSelectMenu(timezoneSelection);
                                    index++;
                                }
                            }
                        }

                        await channel.SendMessageAsync(embed: questionEmbed.Build(),
                            components: componentBuilder.Build());

                        // wait until they respond before sending the next question
                        await TaskWait.Until(() => questionAnsweredUsers.Contains(user.Id));
                        questionAnsweredUsers.Remove(user.Id);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            new Thread(Start).Start();
        }

        private List<ulong> waitingForOtherTimezone = new();

        public async Task ResponseListener(SocketMessage message)
        {
            if (HasOngoingSubmission(message.Author, out BsonDocument? ongoingSubmission))
            {
                if ((ulong) ongoingSubmission!.GetElement("channel_id").Value.AsInt64 != message.Channel.Id)
                    return;

                SocketGuild guild = (message.Channel as SocketGuildChannel)!.Guild;
                
                if (waitingForOtherTimezone.Contains(message.Author.Id))
                {
                    // save it
                    await Collection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("discord_id", message.Author.Id),
                            Builders<BsonDocument>.Filter.Eq("status", Status.Ongoing.ToString())),
                        Builders<BsonDocument>.Update.Set("timezone", message.Content)
                    );
                    
                    OverwritePermissions userPerms = new(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        sendMessages: PermValue.Deny
                    );

                    await (message.Channel as SocketGuildChannel)!.AddPermissionOverwriteAsync(message.Author, userPerms);
                    
                    waitingForOtherTimezone.Remove(message.Author.Id);

                    await FinishSubmission(guild, message.Channel, message.Author);
                    return;
                }
                
                if (!Regex.IsMatch(message.Content, "^\\w{3,16}$"))
                {
                    HauyaEmbedBuilder invalidEmbed = new HauyaEmbedBuilder()
                        .WithRoleColor(guild.GetUser(Bot.User.Id))
                        .WithInformation("Invalid username", "This username is not valid! Try again...")
                        .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", guild)
                        .WithCurrentTimestamp();

                    await (message as SocketUserMessage).ReplyAsync(embed: invalidEmbed.Build());
                    
                    //await (message as SocketUserMessage).ReplyAsync("This username is not valid! Try again...");
                    
                    return;
                }

                string? uuid;
                
                using (HttpClient client = new())
                {
                    string result = await client.GetStringAsync("https://api.mojang.com/users/profiles/minecraft/" + message.Content);
                    
                    try
                    {
                        JObject json = JObject.Parse(result);
                        uuid = json.GetValue("id")?.ToString();
                    }
                    catch (JsonReaderException)
                    {
                        uuid = null;
                    }
                }
                
                if (uuid == null)
                {
                    HauyaEmbedBuilder invalidEmbed = new HauyaEmbedBuilder()
                        .WithRoleColor(guild.GetUser(Bot.User.Id))
                        .WithInformation("Nonexistent username", "This username does not exist! Try again...")
                        .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", guild)
                        .WithCurrentTimestamp();

                    await (message as SocketUserMessage).ReplyAsync(embed: invalidEmbed.Build());
                    
                    //await (message as SocketUserMessage).ReplyAsync("This username does not exist! Try again...");
                    return;
                }

                await Collection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("discord_id", message.Author.Id),
                        Builders<BsonDocument>.Filter.Eq("status", Status.Ongoing.ToString())),
                    Builders<BsonDocument>.Update.Set("minecraft_uuid", uuid)
                );
                
                questionAnsweredUsers.Add(message.Author.Id);
            }
        }
        
        private enum Status
        {
            None,
            Ongoing,
            Submitted,
            Cancelled
        }

        private async Task FinishSubmission(SocketGuild guild, ISocketMessageChannel channel, IUser user)
        {
            HauyaEmbedBuilder finishEmbed = new HauyaEmbedBuilder()
                .WithRoleColor(guild.GetUser(Bot.User.Id))
                .WithInformation("Submitted", "Thank you for participating in the next Fuchsia event!")
                .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", guild)
                .WithCurrentTimestamp();

            await channel.SendMessageAsync(embed: finishEmbed.Build());
            
            BsonDocument toBeCancelledSubmission = GetOngoingSubmission(user) ?? throw new Exception("the fuck its null bitch?");

            await Collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("discord_id", user.Id),
                    Builders<BsonDocument>.Filter.Eq("status", Status.Ongoing.ToString())),
                Builders<BsonDocument>.Update.Set("status", Status.Submitted.ToString())
            );
                    
            SocketTextChannel toBeDeletedChannel = guild
                .GetTextChannel((ulong) toBeCancelledSubmission.GetElement("channel_id").Value.AsInt64);

            async void DeleteThread()
            {
                try
                {
                    await TaskWait.Until(() => false, timeout: 5000);
                }
                catch (TimeoutException) 
                {
                    await toBeDeletedChannel.DeleteAsync();
                }
            }
            
            new Thread(DeleteThread).Start();
        }
        
        public async Task SelectMenuHandler(SocketMessageComponent component)
        {
            if (component.Data.CustomId == "timezone")
            {
                SocketGuild guild = (component.Channel as SocketGuildChannel)!.Guild;
                
                if (component.Data.Values.First() == "other")
                {
                    Console.WriteLine("other");
                    
                    HauyaEmbedBuilder otherEmbed = new HauyaEmbedBuilder()
                        .WithRoleColor(guild.GetUser(Bot.User.Id))
                        .WithInformation("Other timezone", "Please respond with your timezone.")
                        .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", guild)
                        .WithCurrentTimestamp();

                    await component.RespondAsync(embed: otherEmbed.Build());
                    
                    OverwritePermissions userPerms = new(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        sendMessages: PermValue.Allow
                    );

                    await (component.Channel as SocketGuildChannel)!.AddPermissionOverwriteAsync(component.User, userPerms);
                    
                    waitingForOtherTimezone.Add(component.User.Id);
                }
                else
                {
                    HauyaEmbedBuilder normalTimezoneEmbed = new HauyaEmbedBuilder()
                        .WithRoleColor(guild.GetUser(Bot.User.Id))
                        .WithInformation("Timezone saved", "Saved timezone of " + component.Data.Values.First())
                        .WithDatabaseCommonFooter(Bot.Configuration, "fuchsia_minecraft", guild)
                        .WithCurrentTimestamp();

                    await component.RespondAsync(embed: normalTimezoneEmbed.Build(), ephemeral: true);
                    
                    await Collection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("discord_id", component.User.Id),
                            Builders<BsonDocument>.Filter.Eq("status", Status.Ongoing.ToString())),
                        Builders<BsonDocument>.Update.Set("timezone", component.Data.Values.First())
                    );
                    await FinishSubmission(guild, component.Channel, component.User);
                }
            }
        }
    }
}