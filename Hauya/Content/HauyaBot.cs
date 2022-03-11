using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Transactions;
using Discord;
using Discord.WebSocket;
using Hauya.Content.Commands;
using Hauya.Content.Handlers;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Tomat.Framework.Core.Bot;

namespace Hauya.Content
{
    public class HauyaBot : DiscordBot
    {
        public IMongoDatabase Database { get; }
        
        public BsonDocument Configuration { get; set; }
        
        public ParticipationHandler Participation { get; }
        
        public HauyaBot(IMongoDatabase database) : base(GetConfig(database).Result.GetElement("token").Value.AsString)
        {
            Database = database;
            Configuration = GetConfig(database).Result;
            Participation = new ParticipationHandler(this, database);
        }

        public static async Task<BsonDocument> GetConfig(IMongoDatabase database)
        {
           IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("config");
           FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter
               .Eq("name", Debugger.IsAttached ? "development" : "production");
           
           BsonDocument? config = await collection.Find(filter).FirstOrDefaultAsync();
           
           if (config == null)
               throw new Exception("Config not found in database with filter of " + filter);

           return config;
        }
        
        public override async Task OnStartAsync()
        {
            await DiscordClient.SetStatusAsync(UserStatus.Online);
            DiscordClient.ButtonExecuted += Participation.ParticipationButtonHandler;
            DiscordClient.MessageReceived += Participation.ResponseListener;
            DiscordClient.SelectMenuExecuted += Participation.SelectMenuHandler;

        }

        public override string GetPrefix(ISocketMessageChannel channel) => Debugger.IsAttached ? "edge." : ".";
    }
}