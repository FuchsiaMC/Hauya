#region License
// Copyright (C) 2021 Tomat and Contributors, MIT License
#endregion

using System;
using Discord.WebSocket;
using Tomat.Framework.Core.Bot;

namespace Tomat.Framework.Core.Services
{
    public interface IService
    {
        IServiceProvider ServiceProvider { get; }

        DiscordShardedClient Client { get; }

        DiscordBot Bot { get; }
    }
}