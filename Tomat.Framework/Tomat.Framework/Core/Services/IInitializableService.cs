﻿#region License
// Copyright (C) 2021 Tomat and Contributors, MIT License
#endregion

using System.Threading.Tasks;

namespace Tomat.Framework.Core.Services
{
    public interface IInitializableService : IService
    {
        Task InitializeAsync();
    }
}