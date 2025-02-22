using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbyStat.Clients.Base;
using EmbyStat.Clients.Base.Converters;
using EmbyStat.Clients.Base.Http;
using EmbyStat.Clients.Tmdb;
using EmbyStat.Common;
using EmbyStat.Common.Converters;
using EmbyStat.Common.Enums;
using EmbyStat.Common.Extensions;
using EmbyStat.Common.Hubs.Job;
using EmbyStat.Common.Models.Entities;
using EmbyStat.Common.Models.Show;
using EmbyStat.Jobs.Jobs.Interfaces;
using EmbyStat.Repositories.Interfaces;
using EmbyStat.Services.Interfaces;
using Hangfire;
using MediaBrowser.Model.Net;
using MoreLinq;

namespace EmbyStat.Jobs.Jobs.Sync
{
    [DisableConcurrentExecution(60 * 60)]
    public class ShowSyncJob : BaseJob, IShowSyncJob
    {
        private readonly IHttpClient _httpClient;
        private readonly IShowRepository _showRepository;
        private readonly ILibraryRepository _libraryRepository;
        private readonly IStatisticsRepository _statisticsRepository;
        private readonly IShowService _showService;
        private readonly ITmdbClient _tmdbClient;

        public ShowSyncJob(IJobHubHelper hubHelper, IJobRepository jobRepository, ISettingsService settingsService,
            IClientStrategy clientStrategy, IShowRepository showRepository,
            ILibraryRepository libraryRepository, IStatisticsRepository statisticsRepository, IShowService showService, ITmdbClient tmdbClient) 
            : base(hubHelper, jobRepository, settingsService, typeof(ShowSyncJob), Constants.LogPrefix.ShowSyncJob)
        {
            _showRepository = showRepository;
            _libraryRepository = libraryRepository;
            _statisticsRepository = statisticsRepository;
            _showService = showService;
            _tmdbClient = tmdbClient;
            Title = jobRepository.GetById(Id).Title;

            var settings = settingsService.GetUserSettings();
            _httpClient = clientStrategy.CreateHttpClient(settings.MediaServer?.ServerType ?? ServerType.Emby);
        }

        public sealed override Guid Id => Constants.JobIds.ShowSyncId;
        public override string JobPrefix => Constants.LogPrefix.ShowSyncJob;
        public override string Title { get; }

        public override async Task RunJobAsync()
        {
            var cancellationToken = new CancellationToken(false);


            if (!Settings.WizardFinished)
            {
                await LogWarning("Media sync task not running because wizard is not yet finished!");
                return;
            }

            if (!IsMediaServerOnline())
            {
                await LogWarning($"Halting task because we can't contact the server on {Settings.MediaServer.FullMediaServerAddress}, please check the connection and try again.");
                return;
            }

            await LogInformation("First delete all existing media and root media libraries from database so we have a clean start.");
            await LogProgress(3);

            var libraries = await GetLibrariesAsync();
            _libraryRepository.AddOrUpdateRange(libraries);
            await LogInformation($"Found {libraries.Count} root items, getting ready for processing");
            await LogProgress(15);

            await ProcessShowsAsync(libraries, cancellationToken);
            await LogProgress(95);

            await CalculateStatistics();
            await LogProgress(100);
        }

        #region Shows
        private async Task ProcessShowsAsync(IEnumerable<Library> libraries, CancellationToken cancellationToken)
        {
            await LogInformation("Lets start processing show");
            var updateStartTime = DateTime.Now;

            var neededLibraries = libraries.Where(x => Settings.ShowLibraries.Any(y => y == x.Id)).ToList();
            var logIncrementBase = Math.Round(40 / (double)neededLibraries.Count, 1);
            foreach (var library in neededLibraries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await PerformShowSyncAsync(library, updateStartTime, logIncrementBase);
            }

            await LogInformation("Removing shows that are no longer present on your server (if any)");
            _showRepository.RemoveShowsThatAreNotUpdated(updateStartTime);
        }

        private async Task PerformShowSyncAsync(Library library, DateTime updateStartTime, double logIncrementBase)
        {
            var showList = _httpClient.GetShows(library.Id);

            var grouped = showList.GroupBy(x => x.TVDB).ToList();
            await LogInformation($"Found {grouped.Count} show for {library.Name} library");

            for (var i = 0; i < grouped.Count; i++)
            {
                var showGroup = grouped[i];

                var show = showGroup.First();
                foreach (var showDto in showGroup)
                {
                    var seasons = _httpClient.GetSeasons(showDto.Id);
                    var episodes = _httpClient.GetEpisodes(seasons.Select(x => x.Id), show.Id);

                    show.Seasons.AddRange(seasons.Where(x => show.Seasons.All(y => y.Id != x.Id)));

                    episodes.ForEach(x => x.SeasonIndexNumber = seasons.FirstOrDefault(y => y.Id == x.ParentId)?.IndexNumber );
                    show.Episodes.AddRange(episodes.Where(x => show.Episodes.All(y => y.Id != x.Id)));

                }

                show.CumulativeRunTimeTicks = show.Episodes.Sum(x => x.RunTimeTicks ?? 0);
                show.LastUpdated = updateStartTime;
                show.SizeInMb = show.GetShowSize();

                var oldShow = _showRepository.GetShowById(show.Id);

                if (oldShow != null)
                {
                    show.ExternalSyncFailed = oldShow.ExternalSyncFailed;
                    show.ExternalSynced = oldShow.ExternalSynced;
                }

                if (show.TMDB.HasValue)
                {
                    if (show.HasShowChangedEpisodes(oldShow) 
                             || oldShow != null && oldShow.NeedsShowSync())
                    {
                        show = await GetMissingEpisodesFromProviderAsync(show);
                    }
                }

                _showRepository.InsertShow(show);
                await LogInformation($"Processed ({i + 1}/{grouped.Count}) {show.Name} => {show.Seasons.Count} seasons, {show.GetEpisodeCount(true, LocationType.Disk)} episodes, (Size in GB: {Math.Round(show.SizeInMb / 1024, 2)})");
                await LogProgressIncrement(logIncrementBase / grouped.Count);
            }
        }

        private async Task<Show> GetMissingEpisodesFromProviderAsync(Show show)
        {
            try
            {
                return await ProcessMissingEpisodesAsync(show);
            }
            catch (HttpException e)
            {
                show.ExternalSyncFailed = true;

                if (e.Message.Contains("404 Not Found"))
                {
                    await LogWarning($"Can't seem to find {show.Name} on Imdb, skipping show for now");
                }
                return show;
            }
            catch (Exception e)
            {
                await LogError($"Can't seem to process show {show.Name}, check the logs for more details!");
                Logger.Error(e);
                show.ExternalSyncFailed = true;
                return show;
            }
        }

        private async Task<Show> ProcessMissingEpisodesAsync(Show show)
        {
            var missingEpisodesCount = 0;
            var externalEpisodes = await _tmdbClient.GetEpisodesAsync(show.TMDB);

            if (externalEpisodes == null)
            {
                await LogWarning($"Could not find show {show.Name} with id {show.TMDB}");
                return show;
            }


            foreach (var externalEpisode in externalEpisodes)
            {
                var season = show.Seasons.FirstOrDefault(x => x.IndexNumber == externalEpisode.SeasonNumber);

                if (season == null)
                {
                    Logger.Debug($"No season with index {externalEpisode.SeasonNumber} found for missing episode ({show.Name}), so we need to create one first");
                    season = externalEpisode.SeasonNumber.ConvertToSeason(show);
                    show.Seasons.Add(season);
                }

                if (IsEpisodeMissing(show.Episodes, season, externalEpisode))
                {
                    var episode = externalEpisode.ConvertToEpisode(show, season);
                    Logger.Debug($"Episode missing: { episode.Id } - {episode.ParentId} - {episode.ShowId} - {episode.ShowName}");
                    show.Episodes.Add(episode);
                    missingEpisodesCount++;
                }
            }

            await LogInformation($"Found {missingEpisodesCount} missing episodes for show {show.Name}");

            show.Episodes = show.Episodes.Where(x => show.Seasons.Any(y => y.Id.ToString() == x.ParentId)).ToList();
            show.ExternalSynced = true;
            return show;
        }

        private static bool IsEpisodeMissing(IEnumerable<Episode> localEpisodes, Season season, VirtualEpisode tvdbEpisode)
        {
            if (season == null)
            {
                return true;
            }

            foreach (var localEpisode in localEpisodes)
            {
                if (localEpisode.ParentId != season.Id) continue;
                if (!localEpisode.IndexNumberEnd.HasValue)
                {
                    if (localEpisode.IndexNumber == tvdbEpisode.EpisodeNumber)
                    {
                        return false;
                    }
                }
                else
                {
                    if (localEpisode.IndexNumber <= tvdbEpisode.EpisodeNumber &&
                        localEpisode.IndexNumberEnd >= tvdbEpisode.EpisodeNumber)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Helpers

        private bool IsMediaServerOnline()
        {
            _httpClient.BaseUrl = Settings.MediaServer.FullMediaServerAddress;
            return _httpClient.Ping();
        }

        private async Task CalculateStatistics()
        {
            await LogInformation("Calculating show statistics");
            _statisticsRepository.MarkShowTypesAsInvalid();
            _showService.CalculateShowStatistics(new List<string>(0));
            _showService.CalculateCollectedRows(new List<string>(0));

            await LogInformation($"Calculations done");
            await LogProgress(100);
        }

        private async Task<List<Library>> GetLibrariesAsync()
        {
            await LogInformation("Asking MediaServer for all root folders");
            var rootItems = _httpClient.GetMediaFolders();

            Logger.Debug("Following root items are found:");
            rootItems.Items.ForEach(x => Logger.Debug(x.ToString()));

            return rootItems.Items
                .Where(x => x.CollectionType.ToLibraryType() == LibraryType.TvShow)
                .Select(LibraryConverter.ConvertToLibrary)
                .Where(x => x.Type != LibraryType.BoxSets)
                .ToList();
        }

        #endregion
    }
}
