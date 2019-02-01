using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace DurableTask.CosmosDb.Common
{
    public class CosmosDbConnectionProperties 
    {
        /// <summary>
        /// Default timeout in minutes.
        /// </summary>
        private const int DefaultTimeoutInMinutes = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDbConnectionProperties"/> class.
        /// </summary>
        /// <param name="uri"> Cosmos DB service endpoint. </param>
        /// <param name="primaryAuthorizationKey"> The primary authorization key to use when creating a Cosmos DB client. </param>
        /// <param name="databaseName"> Database name. </param>
        /// <param name="collectionName"> Collection name. </param>
        /// <param name="locations"> Location preferences for reads. </param>
        /// <param name="failoverAuthorizationKey"> The failover authorization key to use when creating a Cosmos DB client. </param>
        /// <param name="concurrentUpdateRetryCount">Concurrent Update retry count.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="storageAccountConnectionString">Storage account connection string.</param>
        /// <param name="storageAccountFailoverConnectionString">Storage account failover connection string.</param>
        /// <param name="includedFieldsFromBlob">the included fields from Azure blob which will be persisted in Cosmos DB even if the document is larger than <see cref="ICosmosDbConnectionProperties.MaxDocumentSizeInCosmosDB"/>.</param>
        /// <param name="excludedFieldsFromBlob">the excluded fields from Azure blob which will NOT be persisted in Cosmos DB if the document is larger than <see cref="ICosmosDbConnectionProperties.MaxDocumentSizeInCosmosDB"/>.</param>
        public CosmosDbConnectionProperties(string uri,
                                            string primaryAuthorizationKey,
                                            string databaseName,
                                            string collectionName,
                                            IEnumerable<string> locations,
                                            int concurrentUpdateRetryCount = -1,
                                            TimeSpan? timeout = null)
        {
            this.EndpointUri = new Uri(uri);
            this.PrimaryAuthorizationKey = primaryAuthorizationKey;
            this.DatabaseUri = UriFactory.CreateDatabaseUri(databaseName);
            this.CollectionUri = UriFactory.CreateDocumentCollectionUri(databaseName, collectionName);
            this.DatabaseName = databaseName;
            this.CollectionName = collectionName;
            this.ConcurrentUpdateRetryCount = concurrentUpdateRetryCount;
            this.ConnectionPolicy = new ConnectionPolicy
            {
                //// TODO: arunmann: Need to incorporate other properties supported by ConnectionPolicy
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                RetryOptions = new RetryOptions(),
                RequestTimeout = timeout ?? TimeSpan.FromMinutes(DefaultTimeoutInMinutes)
            };

            if (locations != null)
            {
                foreach (string location in locations)
                {
                    this.ConnectionPolicy.PreferredLocations.Add(location);
                }
            }
        }

        /// <summary>
        /// Gets collection name.
        /// </summary>
        public string CollectionName { get; set; }

        /// <summary>
        /// Gets collection link.
        /// </summary>
        public Uri CollectionUri { get; }

        /// <summary>
        /// Gets the concurrent update retry count.
        /// </summary>
        public int ConcurrentUpdateRetryCount { get; }

        /// <summary>
        /// Gets or sets connection policy.
        /// </summary>
        public ConnectionPolicy ConnectionPolicy { get; set; }

        /// <summary>
        /// Gets database name.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Gets database link.
        /// </summary>
        public Uri DatabaseUri { get; }

        /// <summary>
        /// Gets Cosmos DB service endpoint.
        /// </summary>
        public Uri EndpointUri { get; }

                
        /// <summary>
        /// Gets the Maximum document size to be stored in cosmos DB.
        /// </summary>
        public long MaxDocumentSizeInCosmosDB { get; } = (512 + 1024) * 1024; // 1.5 MB

        /// <summary>
        /// Gets the primary authorization key to use when creating a Cosmos DB client.
        /// </summary>
        public string PrimaryAuthorizationKey { get; }

        /// <summary>
        /// Gets or sets the query maximum degree of parallelism.
        /// </summary>
        public int QueryMaxDegreeOfParallelism { get; set; } = -1;

        /// <summary>
        /// Gets or sets the query maximum item count.
        /// </summary>
        public int QueryMaxItemCount { get; set; } = -1;

        /// <summary>
        /// Gets a fingerprint based on the connection properties.
        /// </summary>
        /// <param name="failoverCredentials">Flag for using the failover credentials.</param>
        /// <returns>Fingerprint string.</returns>
        public string GetFingerprint()
        {
            // For now we will use just the endpoint URI to uniquely identify a connection, but that may change in the future.
            return FormattableString.Invariant($"{this.EndpointUri}-{("Primary")}");
        }
    }
}
