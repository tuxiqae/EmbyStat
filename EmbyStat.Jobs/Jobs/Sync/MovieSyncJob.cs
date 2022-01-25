﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbyStat.Clients.Base;
using EmbyStat.Clients.Base.Converters;
using EmbyStat.Clients.Base.Http;
using EmbyStat.Common;
using EmbyStat.Common.Converters;
using EmbyStat.Common.Enums;
using EmbyStat.Common.Extensions;
using EmbyStat.Common.Hubs.Job;
using EmbyStat.Common.Models.Entities;
using EmbyStat.Common.Models.Net;
using EmbyStat.Common.Models.Settings;
using EmbyStat.Common.SqLite;
using EmbyStat.Jobs.Jobs.Interfaces;
using EmbyStat.Repositories.Interfaces;
using EmbyStat.Services.Interfaces;
using Hangfire;
using MediaBrowser.Model.Querying;
using MoreLinq;

namespace EmbyStat.Jobs.Jobs.Sync
{
    [DisableConcurrentExecution(60 * 60)]
    public class MovieSyncJob : BaseJob, IMovieSyncJob
    {
        private readonly IHttpClient _httpClient;
        private readonly IMovieRepository _movieRepository;
        private readonly IStatisticsRepository _statisticsRepository;
        private readonly IMovieService _movieService;
        private readonly IGenreRepository _genreRepository;
        private readonly IPersonRepository _personRepository;

        public MovieSyncJob(IJobHubHelper hubHelper, IJobRepository jobRepository,
            ISettingsService settingsService, IClientStrategy clientStrategy,
            IMovieRepository movieRepository, IStatisticsRepository statisticsRepository, 
            IMovieService movieService, IGenreRepository genreRepository, IPersonRepository personRepository) 
            : base(hubHelper, jobRepository, settingsService, typeof(MovieSyncJob), Constants.LogPrefix.MovieSyncJob)
        {
            _movieRepository = movieRepository;
            _statisticsRepository = statisticsRepository;
            _movieService = movieService;
            _genreRepository = genreRepository;
            _personRepository = personRepository;

            var settings = settingsService.GetUserSettings();
            _httpClient = clientStrategy.CreateHttpClient(settings.MediaServer?.ServerType ?? ServerType.Emby);
            Title = jobRepository.GetById(Id).Title;
        }

        public sealed override Guid Id => Constants.JobIds.MovieSyncId;
        public override string Title { get; }
        public override string JobPrefix => Constants.LogPrefix.MovieSyncJob;
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


            await ProcessGenresAsync(cancellationToken);
            Console.WriteLine("GENRES DONE");
            await ProcessPeopleAsync(cancellationToken);
            Console.WriteLine("PEOPLE DONE");

            await LogProgress(15);

            await ProcessMoviesAsync(cancellationToken);
            Console.WriteLine("MOVIES DONE");

            await LogProgress(55);

            await CalculateStatistics();
            await LogProgress(100);
        }

        private async Task ProcessGenresAsync(CancellationToken cancellationToken)
        {
            var genres = await _httpClient.GetGenres();
            cancellationToken.ThrowIfCancellationRequested();
            await _genreRepository.UpsertRange(genres);
        }

        private async Task ProcessPeopleAsync(CancellationToken cancellationToken)
        {
            var totalCount = await _httpClient.GetPeopleCount();
            
            const int limit = 25000;
            var processed = 0;
            var j = 0;

            do
            {
                var result = await _httpClient.GetPeople(j * limit, limit);
                var people = result.Items
                    .Select(x => x.ConvertToPeople(Logger))
                    .ToList();
                cancellationToken.ThrowIfCancellationRequested();
                await _personRepository.UpsertRange(people);

                processed += limit;
                j++;
            } while (processed < totalCount);
        }

        private async Task ProcessMoviesAsync(CancellationToken cancellationToken)
        {
            await LogInformation("Lets start processing movies");
            await LogInformation($"{Settings.MovieLibraries.Count} libraries are selected, getting ready for processing");

            var logIncrementBase = Math.Round(40 / (double)Settings.MovieLibraries.Count, 1);
            var genres = await _genreRepository.GetAll();

            foreach (var library in Settings.MovieLibraries)
            {
                var totalCount = await _httpClient.GetMovieCount(library.Id, library.LastSynced);
                if (totalCount == 0)
                {
                    continue;
                }
                var increment = logIncrementBase / (totalCount / (double)100);

                await LogInformation($"Found {totalCount} changed movies since last sync in {library.Name}");
                var processed = 0;
                var j = 0;
                const int limit = 50;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await FetchMoviesAsync(library, j * limit, limit);

                    var movies = result.Items
                        .Where(x => x is {MediaType: "Video"})
                        .Select(x => x.ConvertToMovie(library.Id, genres.ToList(), Logger))
                        .ToList();

                    try
                    {
                        await _movieRepository.UpsertRange(movies);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed to save or update movies");
                    }

                    processed += limit;
                    j++;
                    var logProcessed = processed < totalCount ? processed : totalCount;
                    await LogInformation($"Processed { logProcessed } / { totalCount } movies");
                    await LogProgressIncrement(increment);
                } while (processed < totalCount);
                await SettingsService.UpdateLibrarySyncDate(library.Id, DateTime.UtcNow);
            }
        }

        private async Task<QueryResult<BaseItemDto>> FetchMoviesAsync(LibraryContainer library, int startIndex, int limit)
        {
            try
            { 
                return await _httpClient.GetMovies(library.Id, library.Id, startIndex, limit, library.LastSynced);
            }
            catch (Exception e)
            {
                await LogError($"Movie error: {e.Message}");
                throw;
            }
        }

        private async Task CalculateStatistics()
        {
            await LogInformation("Calculating movie statistics");
            _statisticsRepository.MarkMovieTypesAsInvalid();
            await LogProgress(67);
            await _movieService.CalculateMovieStatistics(new List<string>(0));

            await LogInformation($"Calculations done");
            await LogProgress(100);
        }

        #region Helpers

        private bool IsMediaServerOnline()
        {
            _httpClient.BaseUrl = Settings.MediaServer.FullMediaServerAddress;
            return _httpClient.Ping();
        }

        #endregion
    }
}
