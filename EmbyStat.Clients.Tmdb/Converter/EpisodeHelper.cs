﻿using EmbyStat.Common.Models.Show;
using TMDbLib.Objects.Search;

namespace EmbyStat.Clients.Tmdb.Converter
{
    public static class EpisodeHelper
    {
        public static VirtualEpisode ConvertToVirtualEpisode(this TvSeasonEpisode episode)
        {
            var virtualEpisode = new VirtualEpisode
            {
                Id = episode.Id.ToString(),
                FirstAired = episode.AirDate,
                Name = episode.Name,
                EpisodeNumber = episode.EpisodeNumber,
                SeasonNumber = episode.SeasonNumber
            };

            return virtualEpisode;
        }
    }
}