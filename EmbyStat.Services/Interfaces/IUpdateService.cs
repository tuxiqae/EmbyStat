﻿using System.Threading.Tasks;
using EmbyStat.Clients.GitHub.Models;
using EmbyStat.Common.Models.Settings;

namespace EmbyStat.Services.Interfaces
{
    public interface IUpdateService
    {
        Task<UpdateResult> CheckForUpdate();
        Task DownloadZipAsync(UpdateResult result);
        Task UpdateServerAsync();
    }
}
