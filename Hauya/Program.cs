using System;
using System.IO;
using dotenv.net;
using Tomat.Framework.Core.Bot;
using Hauya.Content;
using MongoDB.Driver;

DotEnv.Load(new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

string? mongoString = Environment.GetEnvironmentVariable("MONGO_STRING");
if (string.IsNullOrEmpty(mongoString))
{
    throw new Exception("Mongo Database Connection String was not loaded or was found empty.");
}

MongoClient client = new(mongoString);

using DiscordBot discordBot = new HauyaBot(client.GetDatabase("discord"));
await discordBot.StartBot();