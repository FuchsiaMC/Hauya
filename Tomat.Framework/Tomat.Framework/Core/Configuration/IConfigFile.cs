#region License
// Copyright (C) 2021 Tomat and Contributors, MIT License
#endregion

using Tomat.Framework.Core.Bot;

namespace Tomat.Framework.Core.Configuration
{
    public interface IConfigFile : IConfigurable
    {
        DiscordBot Bot { get; }

        string SavePath { get; }
    }
}