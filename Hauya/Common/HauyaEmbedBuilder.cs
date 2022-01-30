using System;
using System.Linq;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using Tomat.Framework.Core.Bot;

namespace Hauya.Common
{
    public class HauyaEmbedBuilder
    {
        private readonly EmbedBuilder embedBuilder;

        public HauyaEmbedBuilder()
        {
            embedBuilder = new EmbedBuilder();
        }

        public HauyaEmbedBuilder WithRoleColor(SocketGuildUser guildUser)
        {
            embedBuilder.Color = guildUser.Roles.FirstOrDefault(role => role.Color != Color.Default)?
                .Color ?? new Color(35, 40, 95);

            return this;
        }

        public HauyaEmbedBuilder WithDatabaseTitle(BsonDocument data)
        {
            embedBuilder.Title = data.GetElement("title").Value.AsString;
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseDescription(BsonDocument data)
        {
            embedBuilder.Description = data.GetElement("description").Value.AsString;
            return this;
        }
        
        public HauyaEmbedBuilder WithFormattedDatabaseDescription(BsonDocument data, params object[] formatting)
        {
            embedBuilder.Description = string.Format(data.GetElement("description").Value.AsString, formatting);
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseInformation(BsonDocument data)
        {
            embedBuilder.Title = data.GetElement("title").Value.AsString;
            embedBuilder.Description = data.GetElement("description").Value.AsString;
            return this;
        }
        
        public HauyaEmbedBuilder WithInformation(string title, string desc)
        {
            embedBuilder.Title = title;
            embedBuilder.Description = desc;
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseFooter(BsonDocument data, string url)
        {
            embedBuilder.Footer = new EmbedFooterBuilder
            {
                Text = data.GetElement("footer").Value.AsString,
                IconUrl = url
            };
                
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseFooter(BsonDocument data, SocketGuild guild)
        {
            embedBuilder.Footer = new EmbedFooterBuilder
            {
                Text = data.GetElement("footer").Value.AsString,
                IconUrl = guild.IconUrl
            };
                
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseCommonFooter(BsonDocument data, string id, SocketGuild guild)
        {
            embedBuilder.Footer = new EmbedFooterBuilder
            {
                Text = data
                    .GetElement("common").Value.AsBsonDocument
                    .GetElement("footers").Value.AsBsonArray
                    .Values.First(value => value.AsBsonDocument
                            .GetElement("id").Value.AsString == id).AsBsonDocument
                    .GetElement("contents").Value.AsString,
                IconUrl = guild.IconUrl
            };
                
            return this;
        }
        
        public HauyaEmbedBuilder WithFooterGuildIcon(SocketGuild guild)
        {
            embedBuilder.Footer = new EmbedFooterBuilder
            {
                Text = embedBuilder.Footer?.Text ?? "",
                IconUrl = guild.IconUrl
            };
                
            return this;
        }
        
        public HauyaEmbedBuilder WithDatabaseFooter(BsonDocument data)
        {
            embedBuilder.Footer = new EmbedFooterBuilder
            {
                Text = data.GetElement("footer").Value.AsString,
                IconUrl = embedBuilder.Footer?.IconUrl ?? ""
            };
                
            return this;
        }

        public HauyaEmbedBuilder WithCurrentTimestamp()
        {
            embedBuilder.Timestamp = DateTimeOffset.Now;
            return this;
        }
        
        
        public static EmbedBuilder BuildDatabaseEmbed(BsonDocument data, SocketGuild guild, SocketGuildUser guildUser)
        {
            return new EmbedBuilder
            {
                Color = guildUser.Roles
                            .FirstOrDefault(role => role.Color != Color.Default)?.Color
                        ?? new Color(35, 40, 95),

                Title = data.GetElement("title").Value.AsString,
                Description = data.GetElement("description").Value.AsString,

                Footer = new EmbedFooterBuilder
                {
                    Text = data.GetElement("footer").Value.AsString,
                    IconUrl = guild.IconUrl
                },
                
                Timestamp = DateTimeOffset.Now
            };
        }

        public Embed Build()
        {
            return embedBuilder.Build();
        }
    }
}