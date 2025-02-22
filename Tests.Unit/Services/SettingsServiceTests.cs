﻿using System;
using System.Collections.Generic;
using System.IO;
using EmbyStat.Common.Models.Settings;
using EmbyStat.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Tests.Unit.Services
{
    public class SettingsServiceTests
    {
        private readonly SettingsService _subject;
        private Guid DeviceId { get; set; }
        public SettingsServiceTests()
        {
            DeviceId = Guid.NewGuid();

            var rollbar = new EmbyStat.Common.Models.Settings.Rollbar
            {
                AccessToken = "aaaaaaa",
                Environment = "dev"
            };

            var appSettingsMock = new Mock<IOptions<AppSettings>>();
            appSettingsMock.Setup(x => x.Value).Returns(new AppSettings { Version = "0.0.0.0", Dirs = new Dirs { Config = "config" }, Rollbar = rollbar });

            _subject = new SettingsService(appSettingsMock.Object);
        }

        private void SetupSettingsFile()
        {
            var fileSettings = new UserSettings
            {
                Id = DeviceId,
                Version = 3,
                AppName = "EmbyStat",
                AutoUpdate = false,
                KeepLogsCount = 10,
                Language = "en-US",
                MovieLibraries = new List<string>(),
                ShowLibraries = new List<string>()
            };

            var strJson = JsonConvert.SerializeObject(fileSettings, Formatting.Indented);
            Directory.CreateDirectory("config");
            var dir = Path.Combine("config", "usersettings.json");
            File.WriteAllText(dir, strJson);
        }

        [Fact]
        public async void SaveUserSettings()
        {
            SetupSettingsFile();
            var settings = new UserSettings
            {
                Id = DeviceId,
                AppName = "EmbyStat",
                AutoUpdate = false,
                KeepLogsCount = 10,
                Language = "en-US",
                MovieLibraries = new List<string>(),
                ShowLibraries = new List<string>(),
                MediaServer = new MediaServerSettings()
            };

            _subject.LoadUserSettingsFromFile();
            await _subject.SaveUserSettingsAsync(settings);

            var settingsFilePath = Path.Combine("config", "usersettings.json");
            File.Exists(settingsFilePath).Should().BeTrue();
            var loadedSettings = JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(settingsFilePath));

            loadedSettings.Id.Should().Be(DeviceId);
            loadedSettings.AppName.Should().Be("EmbyStat");
            loadedSettings.AutoUpdate.Should().BeFalse();
            loadedSettings.KeepLogsCount.Should().Be(10);
            loadedSettings.Language.Should().Be("en-US");
        }

        [Fact]
        public async void SaveUserSettings_Should_Reset_Base_Url()
        {
            SetupSettingsFile();
            var settings = new UserSettings
            {
                MediaServer = new MediaServerSettings
                {
                    ServerBaseUrl = "/"
                }
            };

            _subject.LoadUserSettingsFromFile();
            await _subject.SaveUserSettingsAsync(settings);

            var settingsFilePath = Path.Combine("config", "usersettings.json");
            File.Exists(settingsFilePath).Should().BeTrue();
            var loadedSettings = JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(settingsFilePath));

            loadedSettings.MediaServer.ServerBaseUrl.Should().BeEmpty();
        }

        [Fact]
        public void GetUserSettings()
        {
            SetupSettingsFile();
            _subject.LoadUserSettingsFromFile();
            var settings = _subject.GetUserSettings();
            settings.Id.Should().Be(DeviceId);
            settings.AppName.Should().Be("EmbyStat");
            settings.AutoUpdate.Should().BeFalse();
            settings.KeepLogsCount.Should().Be(10);
            settings.Language.Should().Be("en-US");
        }

        [Fact]
        public void GetAppSettings_Should_Return_App_Settings()
        {
            var settings = _subject.GetAppSettings();
            settings.Should().NotBeNull();

            settings.Version.Should().Be("0.0.0.0");
            settings.Dirs.Config.Should().Be("config");
        }

        [Fact]
        public void GetUserSettingsVersionWithSettingsFile()
        {
            SetupSettingsFile();
            _subject.LoadUserSettingsFromFile();
            var version = _subject.GetUserSettingsVersion();
            version.Should().Be(3);
        }

        [Fact]
        public void GetUserSettingsVersionWithoutSettingsFile()
        {
            var version = _subject.GetUserSettingsVersion();
            version.Should().Be(0);
        }
    }
}
