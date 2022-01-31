﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EmbyStat.Common;
using EmbyStat.Common.Enums;
using EmbyStat.Common.Extensions;
using EmbyStat.Common.Models.Entities;
using EmbyStat.Common.Models.Entities.Helpers;
using EmbyStat.Common.Models.Query;
using EmbyStat.Common.SqLite;
using EmbyStat.Common.SqLite.Movies;
using EmbyStat.Logging;
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
            IStatisticsRepository statisticsRepository, IJobRepository jobRepository) : base(jobRepository, personService, typeof(MovieService), "MOVIE")
        {
            _movieRepository = movieRepository;
            _libraryRepository = libraryRepository;
            _settingsService = settingsService;
            _statisticsRepository = statisticsRepository;
        }

        public IEnumerable<Library> GetMovieLibraries()
        {
            var settings = _settingsService.GetUserSettings();
            return _libraryRepository.GetLibrariesById(settings.MovieLibraries.Select(x => x.Id));
        }

        public async Task<MovieStatistics> GetStatistics(List<string> libraryIds)
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
                statistics = await CalculateMovieStatistics(libraryIds);
            }

            return statistics;
        }

        public async Task<MovieStatistics> CalculateMovieStatistics(List<string> libraryIds)
        {
            var statistics = new MovieStatistics();

            statistics.Cards = await CalculateCards(libraryIds);
            statistics.TopCards = CalculateTopCards(libraryIds);
            statistics.Charts = await CalculateCharts(libraryIds);
            statistics.People = CalculatePeopleStatistics(libraryIds);
            statistics.Shorts = CalculateShorts(libraryIds);
            statistics.NoImdb = CalculateNoImdbs(libraryIds);
            statistics.NoPrimary = CalculateNoPrimary(libraryIds);

                var json = JsonConvert.SerializeObject(statistics);
            _statisticsRepository.AddStatistic(json, DateTime.UtcNow, StatisticType.Movie, libraryIds);

            return statistics;
        }

        public Task<MovieStatistics> CalculateMovieStatistics(string libraryId)
        {
            return CalculateMovieStatistics(new List<string> { libraryId });
        }

        public bool TypeIsPresent()
        {
            return _movieRepository.Any();
        }

        public async Task<Page<MovieRow>> GetMoviePage(int skip, int take, string sortField, string sortOrder, Filter[] filters, bool requireTotalCount, List<string> libraryIds)
        {
            var rawList = await _movieRepository
                .GetMoviePage(skip, take, sortField, sortOrder, filters, libraryIds);
                
                var list = rawList.Select(x => new MovieRow
                {
                    Id = x.Id,
                    Name = x.Name,
                    AudioLanguages = x.AudioStreams.Select(y => y.Language).ToArray(),
                    Banner = x.Banner,
                    CommunityRating = x.CommunityRating,
                    Container = x.Container,
                    Genres = x.Genres.ToList(),
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
                    SizeInMb = x.MediaSources.FirstOrDefault()?.SizeInMb ?? 0,
                    VideoStreams = x.VideoStreams.ToList(),
                });

            var page = new Page<MovieRow> { Data = list };
            if (requireTotalCount)
            {
                page.TotalCount = await _movieRepository.Count(filters, libraryIds);
            }

            return page;
        }

        public SqlMovie GetMovie(string id)
        {
            return _movieRepository.GetById(id);
        }

        #region Cards

        private async Task<List<Card<string>>> CalculateCards(IReadOnlyList<string> libraryIds)
        {
            var list = new List<Card<string>>();
            list.AddIfNotNull(await CalculateTotalMovieCount(libraryIds));
            list.AddIfNotNull(await CalculateTotalMovieGenres(libraryIds));
            list.AddIfNotNull(CalculateTotalPlayLength(libraryIds));
            list.AddIfNotNull(CalculateTotalDiskSpace(libraryIds));
            return list;
        }

        private Task<Card<string>> CalculateTotalMovieCount(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(async () =>
            {
                var count = await _movieRepository.Count(libraryIds);
                return new Card<string>
                {
                    Title = Constants.Movies.TotalMovies,
                    Value = count.ToString(),
                    Type = CardType.Text,
                    Icon = Constants.Icons.TheatersRoundedIcon
                };
            }, "Calculate total movie count failed:");
        }

        private Task<Card<string>> CalculateTotalMovieGenres(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(async () =>
            {
                var totalGenres = await _movieRepository.GetGenreCount(libraryIds);
                return new Card<string>
                { 
                    Title = Constants.Common.TotalGenres,
                    Value = totalGenres.ToString(),
                    Type = CardType.Text,
                    Icon = Constants.Icons.PoundRoundedIcon
                };
            }, "Calculate total movie genres failed:");
        }

        private Card<string> CalculateTotalPlayLength(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var playLengthTicks = _movieRepository.GetTotalRuntime(libraryIds) ?? 0;
                var playLength = new TimeSpan(playLengthTicks);

                return new Card<string>
                {
                    Title = Constants.Movies.TotalPlayLength,
                    Value = $"{playLength.Days}|{playLength.Hours}|{playLength.Minutes}",
                    Type = CardType.Time,
                    Icon = Constants.Icons.QueryBuilderRoundedIcon
                };
            }, "Calculate total movie play length failed:");
        }

        protected Card<string> CalculateTotalDiskSpace(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var sum = _movieRepository.GetTotalDiskSpace(libraryIds);
                return new Card<string>
                {
                    Value = sum.ToString(CultureInfo.InvariantCulture),
                    Title = Constants.Common.TotalDiskSpace,
                    Type = CardType.Size,
                    Icon = Constants.Icons.StorageRoundedIcon
                };
            }, "Calculate total movie disk space failed:");
        }

        #endregion

        #region TopCards

        private List<TopCard> CalculateTopCards(IReadOnlyList<string> libraryIds)
        {
            var list = new List<TopCard>();
            list.AddIfNotNull(HighestRatedMovie(libraryIds));
            list.AddIfNotNull(LowestRatedMovie(libraryIds));
            list.AddIfNotNull(OldestPremieredMovie(libraryIds)); // TODO <-- high memory
            list.AddIfNotNull(NewestPremieredMovie(libraryIds)); // TODO <-- high memory
            list.AddIfNotNull(ShortestMovie(libraryIds));
            list.AddIfNotNull(LongestMovie(libraryIds));
            list.AddIfNotNull(LatestAddedMovie(libraryIds));
            return list;
        }

        private TopCard HighestRatedMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetHighestRatedMedia(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.HighestRated, "/10", "CommunityRating", false)
                    : null;
            }, "Calculate highest rated movies failed:");
        }

        private TopCard LowestRatedMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetLowestRatedMedia(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.LowestRated, "/10", "CommunityRating", false)
                    : null;
            }, "Calculate oldest premiered movies failed:");
        }


        private TopCard OldestPremieredMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetOldestPremieredMedia(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.OldestPremiered, "COMMON.DATE", "PremiereDate", ValueTypeEnum.Date)
                    : null;
            }, "Calculate oldest premiered movies failed:");
        }

        private TopCard NewestPremieredMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetNewestPremieredMedia(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.NewestPremiered, "COMMON.DATE", "PremiereDate", ValueTypeEnum.Date)
                    : null;
            }, "Calculate newest premiered movies failed:");
        }

        private TopCard ShortestMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var settings = _settingsService.GetUserSettings();
                var toShortMovieTicks = TimeSpan.FromMinutes(settings.ToShortMovie).Ticks;
                var list = _movieRepository.GetShortestMovie(libraryIds, toShortMovieTicks, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.Shortest, "COMMON.MIN", "RunTimeTicks", ValueTypeEnum.Ticks)
                    : null;
            }, "Calculate shortest movies failed:");
        }

        private TopCard LongestMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetLongestMovie(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.Longest, "COMMON.MIN", "RunTimeTicks", ValueTypeEnum.Ticks)
                    : null;
            }, "Calculate longest movies failed:");
        }

        private TopCard LatestAddedMovie(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var list = _movieRepository.GetLatestAddedMedia(libraryIds, 5).ToArray();
                return list.Length > 0
                    ? list.ConvertToSqlTopCard(Constants.Movies.LatestAdded, "COMMON.DATE", "DateCreated",
                        ValueTypeEnum.Date)
                    : null;
            }, "Calculate latest added movies failed:");
        }

        #endregion

        #region Charts

        private async Task<List<Chart>> CalculateCharts(IReadOnlyList<string> libraryIds)
        {
            var list = new List<Chart>();
            list.AddIfNotNull(await CalculateMovieGenreChart(libraryIds));
            list.AddIfNotNull(CalculateMovieRatingChart(libraryIds));
            list.AddIfNotNull(CalculateMoviePremiereYearChart(libraryIds));
            list.AddIfNotNull(await CalculateOfficialRatingChart(libraryIds));

            return list;
        }

        private Task<Chart> CalculateMovieGenreChart(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(async () =>
            {
                var genres = await _movieRepository.GetMovieGenreChartValues(libraryIds);
                var genresData = genres.Select(x => new {Label = x.Key, Val0 = x.Value});

                return new Chart
                {
                    Title = Constants.CountPerGenre,
                    DataSets = JsonConvert.SerializeObject(genresData),
                    SeriesCount = 1
                };
            }, "Calculate genre chart failed:");
        }

        private Chart CalculateMovieRatingChart(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var ratingDataList = _movieRepository.GetCommunityRatings(libraryIds)
                    .GroupBy(x => x.RoundToHalf())
                    .OrderBy(x => x.Key)
                    .ToList();

                for (double i = 0; i < 10; i += 0.5)
                {
                    if (!ratingDataList.Any(x => x.Key == i))
                    {
                        ratingDataList.Add(new ChartGrouping<double?, float?> { Key = i, Capacity = 0 });
                    }
                }

                var ratingData = ratingDataList
                    .Select(x => new { Label = x.Key?.ToString() ?? Constants.Unknown, Val0 = x.Count() })
                    .OrderBy(x => x.Label)
                    .ToList();

                return new Chart
                {
                    Title = Constants.CountPerCommunityRating,
                    DataSets = JsonConvert.SerializeObject(ratingData),
                    SeriesCount = 1
                };
            }, "Calculate rating chart failed:");
        }

        internal Chart CalculateMoviePremiereYearChart(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(() =>
            {
                var yearDataList = _movieRepository
                    .GetPremiereYears(libraryIds)
                    .GroupBy(x => x.RoundToFiveYear())
                    .Where(x => x.Key != null)
                    .OrderBy(x => x.Key)
                    .ToList();

                if (yearDataList.Any())
                {
                    var lowestYear = yearDataList.Where(x => x.Key.HasValue).Min(x => x.Key);
                    var highestYear = yearDataList.Where(x => x.Key.HasValue).Max(x => x.Key);

                    for (var i = lowestYear; i < highestYear; i += 5)
                    {
                        if (yearDataList.All(x => x.Key != i))
                        {
                            yearDataList.Add(new ChartGrouping<int?, DateTime?> { Key = i, Capacity = 0 });
                        }
                    }
                }

                var yearData = yearDataList
                    .Select(x => new { Label = x.Key != null ? $"{x.Key} - {x.Key + 4}" : Constants.Unknown, Val0 = x.Count() })
                    .OrderBy(x => x.Label)
                    .ToList();

                return new Chart
                {
                    Title = Constants.CountPerPremiereYear,
                    DataSets = JsonConvert.SerializeObject(yearData),
                    SeriesCount = 1
                };
            }, "Calculate premiered year chart failed:");
        }

        private Task<Chart> CalculateOfficialRatingChart(IReadOnlyList<string> libraryIds)
        {
            return CalculateStat(async () =>
            {
                var ratings = await _movieRepository.GetOfficialRatingChartValues(libraryIds);
                var ratingData = ratings.Select(x => new { Label = x.Key, Val0 = x.Value });

                return new Chart
                {
                    Title = Constants.CountPerOfficialRating,
                    DataSets = JsonConvert.SerializeObject(ratingData),
                    SeriesCount = 1
                };
            }, "Calculate official movie rating chart failed:");
        }

        #endregion

        #region People

        public PersonStats CalculatePeopleStatistics(IReadOnlyList<string> libraryIds)
        {
            var returnObj = new PersonStats();
            returnObj.Cards.AddIfNotNull(TotalTypeCount(libraryIds, PersonType.Actor, Constants.Common.TotalActors));
            returnObj.Cards.AddIfNotNull(TotalTypeCount(libraryIds, PersonType.Director, Constants.Common.TotalDirectors));
            returnObj.Cards.AddIfNotNull(TotalTypeCount(libraryIds, PersonType.Writer, Constants.Common.TotalWriters));

            returnObj.GlobalCards.AddIfNotNull(GetMostFeaturedPersonAsync(libraryIds, PersonType.Actor, Constants.Common.MostFeaturedActor));
            returnObj.GlobalCards.AddIfNotNull(GetMostFeaturedPersonAsync(libraryIds, PersonType.Director, Constants.Common.MostFeaturedDirector));
            returnObj.GlobalCards.AddIfNotNull(GetMostFeaturedPersonAsync(libraryIds, PersonType.Writer, Constants.Common.MostFeaturedWriter));

            returnObj.MostFeaturedActorsPerGenreCards = GetMostFeaturedActorsPerGenre(libraryIds, 5, "MovieCount");

            return returnObj;
        }


        private Card<string> TotalTypeCount(IReadOnlyList<string> libraryIds, PersonType type, string title)
        {
            return CalculateStat(() =>
            {
                var value = _movieRepository.GetPeopleCount(libraryIds, type);
                return new Card<string>
                {
                    Value = value.ToString(),
                    Title = title,
                    Icon = Constants.Icons.PeopleAltRoundedIcon,
                    Type = CardType.Text
                };
            }, $"Calculate total {type} count failed:");
        }

        private TopCard GetMostFeaturedPersonAsync(IReadOnlyList<string> libraryIds, PersonType type, string title)
        {
            return CalculateStat(() =>
            {
                var people = _movieRepository
                    .GetMostFeaturedPersons(libraryIds, type, 5)
                    .Select(name => PersonService.GetPersonByNameForMovies(name))
                    .Where(x => x != null)
                    .ToArray();

                return people.ConvertToTopCard(title, string.Empty, "MovieCount");
            }, $"Calculate most featured {type} count failed:");
        }

        private List<TopCard> GetMostFeaturedActorsPerGenre(IReadOnlyList<string> libraryIds, int count, string valueSelector)
        {
            return CalculateStat(() =>
            {
                var movies = _movieRepository.GetAll(libraryIds, true).ToList();

                var list = new List<TopCard>();
                var genreList = movies
                    .SelectMany(x => x.Genres)
                    .Distinct()
                    .OrderBy(x => x);

                foreach (var genre in genreList)
                {
                    var selectedMovies = movies.Where(x => x.Genres.Any(y => y == genre));
                    var people = selectedMovies
                        .SelectMany(x => x.MoviePeople)
                        .Where(x => x.Type == PersonType.Actor)
                        .GroupBy(x => x.Person.Name, (name, p) => new { Name = name, Count = p.Count() })
                        .OrderByDescending(x => x.Count)
                        .Select(x => x.Name)
                        .Select(name => PersonService.GetPersonByNameForMovies(name, genre.Name))
                        .Where(x => x != null)
                        .Take(count * 4)
                        .ToArray();

                    list.Add(people.ConvertToTopCard(genre.Name, string.Empty, valueSelector));
                }

                return list.Where(x => x != null).ToList();
            }, $"Calculate most featured actors per genre failed:");
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
