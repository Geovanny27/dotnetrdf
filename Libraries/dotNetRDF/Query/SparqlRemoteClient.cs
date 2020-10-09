﻿/*
// <copyright>
// dotNetRDF is free and open source software licensed under the MIT License
// -------------------------------------------------------------------------
// 
// Copyright (c) 2009-2020 dotNetRDF Project (http://dotnetrdf.org/)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using VDS.RDF.Parsing.Handlers;

namespace VDS.RDF.Query
{
    /// <summary>
    /// A class for connecting to a remote SPARQL endpoint and making queries against it using the System.Net.Http library.
    /// </summary>
    public class SparqlRemoteClient
    {
        private readonly HttpClient _httpClient;
        private const int LongQueryLength = 2048;

        /// <summary>
        /// Gets/Sets the Accept Header sent with ASK/SELECT queries.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Can be used to workaround buggy endpoints which don't like the broad Accept Header that dotNetRDF sends by default.  If not set or explicitly set to null the library uses the default header generated by <see cref="MimeTypesHelper.HttpSparqlAcceptHeader"/>.
        /// </para>
        /// </remarks>
        public string ResultsAcceptHeader { get; set; } = MimeTypesHelper.HttpSparqlAcceptHeader;

        /// <summary>
        /// Gets/Sets the Accept Header sent with CONSTRUCT/DESCRIBE queries.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Can be used to workaround buggy endpoints which don't like the broad Accept Header that dotNetRDF sends by default.  If not set or explicitly set to null the library uses the default header generated by <see cref="MimeTypesHelper.HttpAcceptHeader"/>.
        /// </para>
        /// </remarks>
        public string RdfAcceptHeader { get; set; } = MimeTypesHelper.HttpAcceptHeader;

        public HttpMode HttpMode { get; set; } = HttpMode.Auto;

        /// <summary>
        /// Gets the URI of the remote SPARQL endpoint.
        /// </summary>
        public Uri EndpointUri { get; }

        /// <summary>
        /// Gets the Default Graph URIs for Queries made to the SPARQL Endpoint.
        /// </summary>
        public List<string> DefaultGraphs { get; }

        /// <summary>
        /// Gets the List of Named Graphs used in requests.
        /// </summary>
        public List<string> NamedGraphs { get; }

        /// <summary>
        /// Create a new SPARQL client.
        /// </summary>
        /// <param name="httpClient">The underlying client to use for HTTP requests.</param>
        /// <param name="endpointUri">The URI of the SPARQL endpoint to connect to.</param>
        public SparqlRemoteClient(HttpClient httpClient, Uri endpointUri)
        {
            _httpClient = httpClient;
            EndpointUri = endpointUri;
            DefaultGraphs = new List<string>();
            NamedGraphs = new List<string>();
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return a SPARQL results set.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <returns>The query results.</returns>
        /// <remarks>This method should be  used when processing SPARQL SELECT or ASK queries.</remarks>
        public async Task<SparqlResultSet> QueryWithResultSetAsync(string sparqlQuery)
        {
            var results = new SparqlResultSet();
            await QueryWithResultSetAsync(sparqlQuery, new ResultSetHandler(results));
            return results;
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return a SPARQL results set.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <param name="resultsHandler">The handler to use when parsing the results returned by the server.</param>
        /// <returns>The query results.</returns>
        /// <remarks>This method should be  used when processing SPARQL SELECT or ASK queries.</remarks>
        public async Task QueryWithResultSetAsync(string sparqlQuery, ISparqlResultsHandler resultsHandler)
        {
            await QueryWithResultSetAsync(sparqlQuery, resultsHandler, CancellationToken.None);
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return a SPARQL results set.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <param name="resultsHandler">The handler to use when parsing the results returned by the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The query results.</returns>
        /// <remarks>This method should be  used when processing SPARQL SELECT or ASK queries.</remarks>
        public async Task QueryWithResultSetAsync(
            string sparqlQuery, ISparqlResultsHandler resultsHandler, CancellationToken cancellationToken)
        {
            using var response = await QueryInternal(sparqlQuery, ResultsAcceptHeader, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new RdfQueryException($"Server reports {response.StatusCode}: {response.ReasonPhrase}.");
            }
            var ctype = response.Content.Headers.ContentType;
            var resultsParser = MimeTypesHelper.GetSparqlParser(ctype.MediaType);
            var responseStream = await response.Content.ReadAsStreamAsync();
            using var responseReader = string.IsNullOrEmpty(ctype.CharSet)
                ? new StreamReader(responseStream)
                : new StreamReader(responseStream, Encoding.GetEncoding(ctype.CharSet));
            resultsParser.Load(resultsHandler, responseReader);
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return an RDF Graph.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <returns>An RDF Graph.</returns>
        /// <remarks>This method should be used when processing SPARQL CONSTRUCT or DESCRIBE queries.</remarks>
        public async Task<IGraph> QueryWithResultGraphAsync(string sparqlQuery)
        {
            var g = new Graph {BaseUri = EndpointUri};
            await QueryWithResultGraphAsync(sparqlQuery, new GraphHandler(g), CancellationToken.None);
            return g;
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return an RDF Graph.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <param name="handler">The handler to use when parsing the graph data returned by the server.</param>
        /// <returns>An RDF Graph.</returns>
        /// <remarks>This method should be used when processing SPARQL CONSTRUCT or DESCRIBE queries.</remarks>
        public async Task QueryWithResultGraphAsync(string sparqlQuery, IRdfHandler handler)
        {
            await QueryWithResultGraphAsync(sparqlQuery, handler, CancellationToken.None);
        }

        /// <summary>
        /// Execute a SPARQL query that is intended to return an RDF Graph.
        /// </summary>
        /// <param name="sparqlQuery">The query to be executed.</param>
        /// <param name="handler">The handler to use when parsing the graph data returned by the server.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An RDF Graph.</returns>
        /// <remarks>This method should be used when processing SPARQL CONSTRUCT or DESCRIBE queries.</remarks>
        public async Task QueryWithResultGraphAsync(string sparqlQuery, IRdfHandler handler,
            CancellationToken cancellationToken)
        {
            using var response = await QueryInternal(sparqlQuery, RdfAcceptHeader, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new RdfQueryException($"Server reports {response.StatusCode}: {response.ReasonPhrase}.");
            }
            var ctype = response.Content.Headers.ContentType;
            var rdfParser = MimeTypesHelper.GetParser(ctype.MediaType);
            var responseStream = await response.Content.ReadAsStreamAsync();
            using var responseReader = string.IsNullOrEmpty(ctype.CharSet)
                ? new StreamReader(responseStream)
                : new StreamReader(responseStream, Encoding.GetEncoding(ctype.CharSet));
            rdfParser.Load(handler, responseReader);
        }

        /// <summary>
        /// Internal method which builds and executes the query as a GET or POST as appropriate.
        /// </summary>
        /// <param name="sparqlQuery">The SPARQL query string.</param>
        /// <param name="acceptHeader">The value to insert into the Accept header of the outgoing HTTP request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns></returns>
        protected async Task<HttpResponseMessage> QueryInternal(string sparqlQuery, string acceptHeader, CancellationToken cancellationToken)
        {
            var usePost = HttpMode == HttpMode.Post ||
                          HttpMode == HttpMode.Auto && sparqlQuery.Length >= LongQueryLength;
            var queryUri = new StringBuilder(EndpointUri.AbsoluteUri);
            if (!usePost)
            {
                try
                {
                    queryUri.Append(string.Empty.Equals(EndpointUri.Query) ? "?query=" : "&query=");
                    queryUri.Append(HttpUtility.UrlEncode(sparqlQuery));
                    // Add the Default Graph URIs
                    foreach (var defaultGraph in DefaultGraphs)
                    {
                        if (defaultGraph.Equals(string.Empty)) continue;
                        queryUri.Append("&default-graph-uri=");
                        queryUri.Append(HttpUtility.UrlEncode(defaultGraph));
                    }

                    // Add the Named Graph URIs
                    foreach (var namedGraph in NamedGraphs)
                    {
                        if (namedGraph.Equals(string.Empty)) continue;
                        queryUri.Append("&named-graph-uri=");
                        queryUri.Append(HttpUtility.UrlEncode(namedGraph));
                    }
                }
                catch (UriFormatException)
                {
                    // Query cannot be encoded as an query parameter
                    if (HttpMode == HttpMode.Get) throw;
                    usePost = true;
                }
            }

            if (usePost || queryUri.Length > LongQueryLength)
            {
                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("query", sparqlQuery),
                };
                data.AddRange(DefaultGraphs.Select(g => new KeyValuePair<string, string>("default-graph-uri", g)));
                data.AddRange(NamedGraphs.Select(g => new KeyValuePair<string, string>("named-graph-uri", g)));
                var content = new FormUrlEncodedContent(data);
                var requestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = EndpointUri,
                    Content = content,
                    Headers = { {HttpRequestHeader.Accept.ToString(), acceptHeader} },
                };
                return await _httpClient.SendAsync(requestMessage, cancellationToken);
            }
            else
            {
                var requestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(queryUri.ToString()),
                    Headers = { {HttpRequestHeader.Accept.ToString(), acceptHeader}},
                };
                return await _httpClient.SendAsync(requestMessage, cancellationToken);
            }
        }
    }
}
