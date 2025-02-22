﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EmbyStat.Common;
using EmbyStat.Common.Enums;
using EmbyStat.Common.Extensions;
using EmbyStat.Common.Models.Entities;
using EmbyStat.Common.Models.Entities.Helpers;
using EmbyStat.Common.Models.Query;
using EmbyStat.Repositories.Interfaces;
using EmbyStat.Services.Abstract;
using EmbyStat.Services.Converters;
using EmbyStat.Services.Interfaces;
using EmbyStat.Services.Models.Cards;
using EmbyStat.Services.Models.Charts;
using EmbyStat.Services.Models.DataGrid;
using EmbyStat.Services.Models.Movie;
using EmbyStat.Services.Models.Stat;
using Newtonsoft.Json;

namespace EmbyStat.Services
{
    public class MovieService : MediaService, IMovieService
    {
        private readonly IMovieRepository _movieRepository;
        private readonly ILibraryRepository _libraryRepository;
        private readonly ISettingsService _settingsService;
        private readonly IStatisticsRepository _statisticsRepository;

        public MovieService(IMovieRepository movieRepository, ILibraryRepository libraryRepository,
            IPersonService personService, ISettingsService settingsService,
            IStatisticsRepository statisticsRepository, IJobRepository jobRepository) : base(jobRepository, personService)
        {
            _movieRepository = movieRepository;
            _libraryRepository = libraryRepository;
            _settingsService = settingsService;
            _statisticsRepository = statisticsRepository;
        }

        public IEnumerable<Library> GetMovieLibraries()
        {
            var settings = _settingsService.GetUserSettings();
            return _libraryRepository.GetLibrariesById(settings.MovieLibraries);
        }

        public MovieStatistics GetStatistics(List<string> libraryIds)
        {
            var statistic = _statisticsRepository.GetLastResultByType(StatisticType.Movie, libraryIds);

            MovieStatistics statistics;
            if (StatisticsAreValid(statistic, libraryIds, Constants.JobIds.MovieSyncId))
            {
                statistics = JsonConvert.DeserializeObject<MovieStatistics>(statistic.JsonResult);

                if (!_settingsService.GetUserSettings().ToShortMovieEnabled && statistics.Shorts.Any())
                {
                    statistics.Shorts = new List<ShortMovie>(0);
                }
            }
            else
            {
                statistics = CalculateMovieStatistics(libraryIds);
            }

            return statistics;
        }

        public MovieStatistics CalculateMovieStatistics(List<string> libraryIds)
        {
            var statistics = new MovieStatistics
            {
                Cards = CalculateCards(libraryIds),
                TopCards = CalculateTopCards(libraryIds),
                Charts = CalculateCharts(libraryIds),
                People = CalculatePeopleStatistics(libraryIds),
                Shorts = CalculateShorts(libraryIds),
                NoImdb = CalculateNoImdbs(libraryIds),
                NoPrimary = CalculateNoPrimary(libraryIds),
            };

            var json = JsonConvert.SerializeObject(statistics);
            _statisticsRepository.AddStatistic(json, DateTime.UtcNow, StatisticType.Movie, libraryIds);

            return statistics;
        }

        public MovieStatistics CalculateMovieStatistics(string libraryId)
        {
            return CalculateMovieStatistics(new List<string> { libraryId });
        }

        public bool TypeIsPresent()
        {
            return _movieRepository.Any();
        }
        public Page<MovieRow> GetMoviePage(int skip, int take, string sortField, string sortOrder, Filter[] filters, bool requireTotalCount, List<string> libraryIds)
        {
            var list = _movieRepository
                .GetMoviePage(skip, take, sortField, sortOrder, filters, libraryIds)
                .Select(x => new MovieRow
                {
                    Id = x.Id,
                    Name = x.Name,
                    AudioLanguages = x.AudioStreams.Select(y => y.Language).ToArray(),
                    Banner = x.Banner,
                    CommunityRating = x.CommunityRating,
                    Container = x.Container,
                    Genres = x.Genres,
                    IMDB = x.IMDB,
                    TVDB = x.TVDB,
                    Logo = x.Logo,
                    OfficialRating = x.OfficialRating,
                    Path = x.Path,
                    PremiereDate = x.PremiereDate,
                    Primary = x.Primary,
                    RunTime = Math.Round((decimal)(x.RunTimeTicks ?? 0) / 600000000),
                    SortName = x.SortName,
                    Subtitles = x.SubtitleStreams.Select(y => y.Language).ToArray(),
                    TMDB = x.TMDB,
                    Thumb = x.Thumb,
                    Height = x.VideoStreams.FirstOrDefault()?.Height,
                    Width = x.VideoStreams.FirstOrDefault()?.Width,
                    SizeInMb = x.MediaSources.FirstOrDefault()?.SizeInMb ?? 0,
                    BitRate = Math.Round((x.VideoStreams.FirstOrDefault()?.BitRate ?? 0d) / 1048576, 2),
                    Codec = x.VideoStreams.FirstOrDefault()?.Codec,
                    BitDepth = x.VideoStreams.FirstOrDefault()?.BitDepth,
                    VideoRange = x.VideoStreams.FirstOrDefault()?.VideoRange
                });

            var page = new Page<MovieRow> { Data = list };
            if (requireTotalCount)
            {
                page.TotalCount = _movieRepository.GetMediaCount(filters, libraryIds);
            }

            return page;
        }

        public Movie GetMovie(string id)
        {
            return _movieRepository.GetMovieById(id);
        }

        #region Cards

        private List<Card<string>> CalculateCards(IReadOnlyList<string> libraryIds)
        {
            return new List<Card<string>>
            {
                CalculateTotalMovieCount(libraryIds),
                CalculateTotalMovieGenres(libraryIds),
                CalculateTotalPlayLength(libraryIds),
                CalculateTotalDiskSize(libraryIds),
            };
        }

        private Card<string> CalculateTotalMovieCount(IReadOnlyList<string> libraryIds)
        {
            var count = _movieRepository.GetMediaCount(libraryIds);
            return new Card<string>
            {
                Title = Constants.Movies.TotalMovies,
                Value = count.ToString(),
                Type = CardType.Text,
                Icon = Constants.Icons.TheatersRoundedIcon
            };
        }

        private Card<string> CalculateTotalMovieGenres(IReadOnlyList<string> libraryIds)
        {
            var totalGenres = _movieRepository.GetGenreCount(libraryIds);
            return new Card<string>
            {
                Title = Constants.Common.TotalGenres,
                Value = totalGenres.ToString(),
                Type = CardType.Text,
                Icon = Constants.Icons.PoundRoundedIcon
            };
        }

        private Card<string> CalculateTotalPlayLength(IReadOnlyList<string> libraryIds)
        {
            var playLengthTicks = _movieRepository.GetTotalRuntime(libraryIds);
            var playLength = new TimeSpan(playLengthTicks);

            return new Card<string>
            {
                Title = Constants.Movies.TotalPlayLength,
                Value = $"{playLength.Days}|{playLength.Hours}|{playLength.Minutes}",
                Type = CardType.Time,
                Icon = Constants.Icons.QueryBuilderRoundedIcon
            };
        }

        protected Card<string> CalculateTotalDiskSize(IReadOnlyList<string> libraryIds)
        {
            var sum = _movieRepository.GetTotalDiskSize(libraryIds);
            return new Card<string>
            {
                Value = sum.ToString(CultureInfo.InvariantCulture),
                Title = Constants.Common.TotalDiskSize,
                Type = CardType.Size,
                Icon = Constants.Icons.StorageRoundedIcon
            };
        }

        #endregion

        #region TopCards

        private List<TopCard> CalculateTopCards(IReadOnlyList<string> libraryIds)
        {
            var list = new List<TopCard>();
            var highestRatedMovie = HighestRatedMovie(libraryIds);
            list.AddIfNotNull(highestRatedMovie);

            var lowestRatedMovie = LowestRatedMovie(libraryIds);
            list.AddIfNotNull(lowestRatedMovie);

            var oldestPremieredMovie = OldestPremieredMovie(libraryIds);
            list.AddIfNotNull(oldestPremieredMovie);

            var newestPremieredMovie = NewestPremieredMovie(libraryIds);
            list.AddIfNotNull(newestPremieredMovie);

            var shortestMovie = ShortestMovie(libraryIds);
            list.AddIfNotNull(shortestMovie);

            var longestMovie = LongestMovie(libraryIds);
            list.AddIfNotNull(longestMovie);

            var latestAddedMovie = LatestAddedMovie(libraryIds);
            list.AddIfNotNull(latestAddedMovie);

            return list;
        }

        private TopCard HighestRatedMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetHighestRatedMedia(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.HighestRated, "/10", "CommunityRating", false)
                : null;
        }

        private TopCard LowestRatedMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetLowestRatedMedia(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.LowestRated, "/10", "CommunityRating", false)
                : null;
        }


        private TopCard OldestPremieredMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetOldestPremieredMedia(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.OldestPremiered, "COMMON.DATE", "PremiereDate", ValueTypeEnum.Date)
                : null;
        }

        private TopCard NewestPremieredMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetNewestPremieredMedia(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.NewestPremiered, "COMMON.DATE", "PremiereDate", ValueTypeEnum.Date)
                : null;
        }

        private TopCard ShortestMovie(IReadOnlyList<string> libraryIds)
        {
            var settings = _settingsService.GetUserSettings();
            var toShortMovieTicks = TimeSpan.FromMinutes(settings.ToShortMovie).Ticks;
            var list = _movieRepository.GetShortestMovie(libraryIds, toShortMovieTicks, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.Shortest, "COMMON.MIN", "RunTimeTicks", ValueTypeEnum.Ticks)
                : null;
        }

        private TopCard LongestMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetLongestMovie(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.Longest, "COMMON.MIN", "RunTimeTicks", ValueTypeEnum.Ticks)
                : null;
        }

        private TopCard LatestAddedMovie(IReadOnlyList<string> libraryIds)
        {
            var list = _movieRepository.GetLatestAddedMedia(libraryIds, 5).ToArray();

            return list.Length > 0
                ? list.ConvertToTopCard(Constants.Movies.LatestAdded, "COMMON.DATE", "DateCreated", ValueTypeEnum.Date)
                : null;
        }

        #endregion

        #region Charts

        private List<Chart> CalculateCharts(IReadOnlyList<string> libraryIds)
        {
            var movies = _movieRepository.GetAll(libraryIds);
            return new List<Chart>
            {
                CalculateGenreChart(movies),
                CalculateRatingChart(movies.Select(x => x.CommunityRating)),
                CalculatePremiereYearChart(movies.Select(x => x.PremiereDate)),
                CalculateOfficialRatingChart(movies)
            };
        }

        private Chart CalculateOfficialRatingChart(IEnumerable<Movie> movies)
        {
            var ratingData = movies
                .Where(x => !string.IsNullOrWhiteSpace(x.OfficialRating))
                .GroupBy(x => x.OfficialRating.ToUpper())
                .Select(x => new { Label = x.Key, Val0 = x.Count() })
                .OrderBy(x => x.Label)
                .ToList();

            return new Chart
            {
                Title = Constants.CountPerOfficialRating,
                DataSets = JsonConvert.SerializeObject(ratingData),
                SeriesCount = 1
            };
        }

        #endregion

        #region People

        public PersonStats CalculatePeopleStatistics(IReadOnlyList<string> libraryIds)
        {
            var returnObj = new PersonStats();
            try
            {
                returnObj.Cards = new List<Card<string>>
                {
                    TotalTypeCount(libraryIds, PersonType.Actor, Constants.Common.TotalActors),
                    TotalTypeCount(libraryIds, PersonType.Director, Constants.Common.TotalDirectors),
                    TotalTypeCount(libraryIds, PersonType.Writer, Constants.Common.TotalWriters),
                };

                returnObj.GlobalCards = new List<TopCard>();

                var mostFeaturedActor = GetMostFeaturedPersonAsync(libraryIds, PersonType.Actor, Constants.Common.MostFeaturedActor);
                if (mostFeaturedActor != null)
                {
                    returnObj.GlobalCards.Add(mostFeaturedActor);
                }

                var mostFeaturedDirector = GetMostFeaturedPersonAsync(libraryIds, PersonType.Director, Constants.Common.MostFeaturedDirector);
                if (mostFeaturedDirector != null)
                {
                    returnObj.GlobalCards.Add(mostFeaturedDirector);
                }

                var mostFeaturedWriter = GetMostFeaturedPersonAsync(libraryIds, PersonType.Writer, Constants.Common.MostFeaturedWriter);
                if (mostFeaturedWriter != null)
                {
                    returnObj.GlobalCards.Add(mostFeaturedWriter);
                }

                returnObj.MostFeaturedActorsPerGenreCards = GetMostFeaturedActorsPerGenreAsync(libraryIds);

                return returnObj;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        private Card<string> TotalTypeCount(IReadOnlyList<string> libraryIds, PersonType type, string title)
        {
            var value = _movieRepository.GetPeopleCount(libraryIds, type);
            return new Card<string>
            {
                Value = value.ToString(),
                Title = title,
                Icon = Constants.Icons.PeopleAltRoundedIcon,
                Type = CardType.Text
            };
        }

        private TopCard GetMostFeaturedPersonAsync(IReadOnlyList<string> libraryIds, PersonType type, string title)
        {
            var people = _movieRepository
                    .GetMostFeaturedPersons(libraryIds, type, 5)
                    .Select(name => PersonService.GetPersonByNameForMovies(name))
                    .Where(x => x != null)
                    .ToArray();

            return people.ConvertToTopCard(title, string.Empty, "MovieCount");

        }

        private List<TopCard> GetMostFeaturedActorsPerGenreAsync(IReadOnlyList<string> libraryIds)
        {
            var movies = _movieRepository.GetAll(libraryIds);
            return GetMostFeaturedActorsPerGenre(movies, 5, "MovieCount");
        }

        private List<TopCard> GetMostFeaturedActorsPerGenre(IReadOnlyList<Extra> media, int count, string valueSelector)
        {
            var list = new List<TopCard>();
            foreach (var genre in media.SelectMany(x => x.Genres).Distinct().OrderBy(x => x))
            {
                var selectedMovies = media.Where(x => x.Genres.Any(y => y == genre));
                var people = selectedMovies
                    .SelectMany(x => x.People)
                    .Where(x => x.Type == PersonType.Actor)
                    .GroupBy(x => x.Name, (name, p) => new { Name = name, Count = p.Count() })
                    .OrderByDescending(x => x.Count)
                    .Select(x => x.Name)
                    .Select(name => PersonService.GetPersonByNameForMovies(name, genre))
                    .Where(x => x != null)
                    .Take(count * 4)
                    .ToArray();

                list.Add(people.ConvertToTopCard(genre, string.Empty, valueSelector));
            }

            return list.Where(x => x != null).ToList();
        }

        #endregion

        #region Suspicious

        private IEnumerable<ShortMovie> CalculateShorts(IReadOnlyList<string> libraryIds)
        {
            var settings = _settingsService.GetUserSettings();
            if (!settings.ToShortMovieEnabled)
            {
                return new List<ShortMovie>(0);
            }

            var shortMovies = _movieRepository.GetToShortMovieList(libraryIds, settings.ToShortMovie);
            return shortMovies.Select((t, i) => new ShortMovie
            {
                Number = i,
                Duration = Math.Floor(new TimeSpan(t.RunTimeTicks ?? 0).TotalMinutes),
                Title = t.Name,
                MediaId = t.Id
            }).ToList();
        }

        private IEnumerable<SuspiciousMovie> CalculateNoImdbs(IReadOnlyList<string> libraryIds)
        {
            var moviesWithoutImdbId = _movieRepository.GetMoviesWithoutImdbId(libraryIds);
            return moviesWithoutImdbId
                .Select((t, i) => new SuspiciousMovie
                {
                    Number = i,
                    Title = t.Name,
                    MediaId = t.Id
                });
        }

        private IEnumerable<SuspiciousMovie> CalculateNoPrimary(IReadOnlyList<string> libraryIds)
        {
            var noPrimaryImageMovies = _movieRepository.GetMoviesWithoutPrimaryImage(libraryIds);
            return noPrimaryImageMovies.Select((t, i) => new SuspiciousMovie
            {
                Number = i,
                Title = t.Name,
                MediaId = t.Id
            })
            .ToList();
        }

        #endregion
    }
}
