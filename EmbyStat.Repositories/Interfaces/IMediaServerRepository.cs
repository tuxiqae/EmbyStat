﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbyStat.Common.Enums;
using EmbyStat.Common.Models.Entities;
using EmbyStat.Common.Models.Entities.Users;

namespace EmbyStat.Repositories.Interfaces;

public interface IMediaServerRepository
{
    #region MediaServer Status
    Task<MediaServerStatus> GetEmbyStatus();
    Task IncreaseMissedPings();
    Task ResetMissedPings();
    #endregion

    #region MediaServer Plugins
    Task<List<PluginInfo>>  GetAllPlugins();
    Task InsertPlugins(IEnumerable<PluginInfo> plugins);
    Task DeleteAllPlugins();
    #endregion

    #region MediaServer Server Info
    Task<MediaServerInfo> GetServerInfo();
    Task DeleteAndInsertServerInfo(MediaServerInfo entity);
    Task DeleteServerInfo();
    #endregion

    #region MediaServer Users
    Task DeleteAndInsertUsers(IEnumerable<MediaServerUser> users);
    Task<MediaServerUser[]>  GetAllUsers();
    Task<MediaServerUser[]>  GetAllAdministrators();
    Task<MediaServerUser> GetUserById(string id);
    Task DeleteAllUsers();
    Task InsertOrUpdateUserViews(List<MediaServerUserView> views);
    #endregion

    #region Devices
    Task<List<Device>> GetAllDevices();
    Task DeleteAndInsertDevices(IEnumerable<Device> devices);
    Task DeleteAllDevices();
    #endregion

    #region Libraries
    Task<List<Library>> GetAllLibraries();
    Task<List<Library>> GetAllLibraries(LibraryType type);
    Task<List<Library>> GetAllLibraries(LibraryType type, bool synced);
    Task SetLibraryAsSynced(string[] libraryIds, LibraryType type);
    Task DeleteAndInsertLibraries(Library[] libraries);
    Task DeleteAllLibraries();
    Task UpdateLibrarySyncDate(string libraryId, DateTime utcNow);
    #endregion
}