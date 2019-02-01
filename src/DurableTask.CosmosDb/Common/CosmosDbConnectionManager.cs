using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DurableTask.CosmosDb.Common
{
    internal sealed class CosmosDbConnectionManager 
    {
        /// <summary>
        /// Object to facilitate lazy loading of connection manager.
        /// </summary>
        private static readonly Lazy<CosmosDbConnectionManager> LazyDbConnectionManager =
            new Lazy<CosmosDbConnectionManager>(() => new CosmosDbConnectionManager());

        /// <summary>
        /// Dictionary of DocumentClient objects. The key is formed using the connection properties
        /// associated with a given <see cref="DocumentClient"/> object and uniquely identifies the latter. 
        /// </summary>
        private readonly ConcurrentDictionary<string, DocumentClient> connections = new ConcurrentDictionary<string, DocumentClient>();

        /// <summary>
        /// Prevents a default instance of the <see cref="CosmosDbConnectionManager"/> class from being created.
        /// </summary>
        private CosmosDbConnectionManager()
        {
        }

        /// <summary>
        /// Gets DbConnectionsManager instance.
        /// </summary>
        public static CosmosDbConnectionManager Instance => LazyDbConnectionManager.Value;

        /// <summary>
        /// Gets the number of connections pooled.
        /// </summary>
        public int NumConnections => this.connections.Count;

        /// <summary>
        /// Gets DocumentClient object based on the connection properties.
        /// </summary>
        /// <param name="connectionProperties">Connection properties.</param>
        /// <returns><see cref="DocumentClient" /> object.</returns>
        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "By design. Document client object needs to live in memory after execution of this method.")]
        public DocumentClient GenerateConnection(CosmosDbConnectionProperties connectionProperties)
        {
            string key = connectionProperties.GetFingerprint();

            if (!this.connections.ContainsKey(key))
            {
                var documentClient = new DocumentClient(
                    connectionProperties.EndpointUri,
                    connectionProperties.PrimaryAuthorizationKey,
                    connectionProperties.ConnectionPolicy);

                //// We intentionally skip running this because OpenAsync will fetch metadata about each partition in every collection for the database.
                //// This causes performance degradation because a single service does not need information about more than the collections it is going to use (1-5) in most services
                //// Document client also tries to open a TCP connection to every cosmos DB partition in every collection, thus exhauting ports 
                //// (SocketException: An operation on a socket could not be performed because the system lacked sufficient buffer space or because a queue was full)
                //// await documentClient.OpenAsync();

                this.connections.GetOrAdd(key, documentClient);
            }

            return this.connections[key];
        }
    }
}
