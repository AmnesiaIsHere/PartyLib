﻿using System.Net;

namespace PartyLib.Bases
{
    public class HttpAssetCore
    {
        /// <summary>
        /// Whether the HTTP request for the asset succeeded
        /// </summary>
        public bool SuccessfulFetch { get; protected set; } = false;

        /// <summary>
        /// Status code for the HTTP request
        /// </summary>
        public HttpStatusCode StatusCode { get; protected set; }
    }
}