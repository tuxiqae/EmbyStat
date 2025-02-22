﻿using System.Collections.Generic;
using System.Threading.Tasks;
using EmbyStat.Clients.Base.Http;
using EmbyStat.Common.Models;
using Microsoft.AspNetCore.Http;
using RestSharp;

namespace EmbyStat.Clients.Jellyfin.Http
{
    public class JellyfinHttpClient : BaseHttpClient, IJellyfinHttpClient
    {
        public JellyfinHttpClient(IRestClient client, IHttpContextAccessor accessor) : base(client, accessor)
        {
            
        }

        public bool Ping()
        {
            return Ping("\"Jellyfin Server\"");
        }

        public Task<IEnumerable<MediaServerUdpBroadcast>> SearchServer()
        {
            return SearchServer("who is JellyfinServer?");
        }
    }
}
