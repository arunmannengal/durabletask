using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DurableTask.CosmosDb.Common
{
    public class CosmosDbProvider
    {
        /// <summary>
        /// Retry interval in sec
        /// </summary>
        private const int RetryIntervalInSecs = 5;

        /// <summary>
        /// List of retriable status codes.
        /// Refer: https://docs.microsoft.com/en-us/rest/api/cosmos-db/http-status-codes-for-cosmosdb
        /// </summary>
        private static readonly ISet<HttpStatusCode> RetriableHttpStatusCodes = new HashSet<HttpStatusCode>
                                                                                    {
                                                                                        (HttpStatusCode)429,
                                                                                        (HttpStatusCode)503
                                                                                    };
        /// <summary>
        /// Cosmos DB connection properties for the single account that will be used.
        /// </summary>
        private readonly CosmosDbConnectionProperties cosmosDbConnectionProperties;

        /// <summary>
        /// The partition key.
        /// </summary>
        private string partitionKey = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDbProvider" /> class.
        /// </summary>
        /// <param name="cosmosDbConnectionProperties">Configuration settings to be used by the provider.</param>
        /// TODO: Remove default null value when all consumers of this provider pass AzureCxpContext
        public CosmosDbProvider(CosmosDbConnectionProperties cosmosDbConnectionProperties)                                
                                
        {
            this.cosmosDbConnectionProperties = cosmosDbConnectionProperties;            
        }

        /// <summary>
        /// Creates a document in Cosmos DB as an asynchronous operation.
        /// </summary>
        /// <param name="jsonObject">The document to be created.</param>
        /// <returns>
        /// The resource returned in the Cosmos DB response.
        /// </returns>
        public async Task<string> CreateAsync(JObject jsonObject)
        {
            var documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);
            ResourceResponse<Document> resourceResponse = null;
            resourceResponse = await documentClient.CreateDocumentAsync(this.cosmosDbConnectionProperties.CollectionUri, jsonObject);

            return resourceResponse?.Resource?.ToString();

        }

        /// <summary>
        /// Creates Collection if it doesn't exist
        /// </summary>
        /// <param name="partitionKeyForCollection">
        /// The partition key.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task CreateCollectionIfNotExists(string partitionKeyForCollection)
        {
            var documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);

            var currentCollection = new DocumentCollection
            {
                Id = this.cosmosDbConnectionProperties.CollectionName
            };

            currentCollection.PartitionKey.Paths.Add($"/{partitionKeyForCollection}");

            await documentClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(this.cosmosDbConnectionProperties.DatabaseName),
                currentCollection,
                new RequestOptions
                {
                    OfferThroughput = 5000
                });
        }

        /// <summary>
        /// Deletes a document from Cosmos DB as an asynchronous operation.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="partitionKeyForCollection">Partition key.</param>
        /// <returns>Task</returns>
        public async Task DeleteAsync(string documentId, string partitionKeyForCollection)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);
            Uri docLink = UriFactory.CreateDocumentUri(
                this.cosmosDbConnectionProperties.DatabaseName,
                this.cosmosDbConnectionProperties.CollectionName,
                documentId);

            ResourceResponse<Document> resourceResponse = null;

            resourceResponse = await documentClient.DeleteDocumentAsync(
                                   docLink,
                                   new RequestOptions
                                   {
                                       PartitionKey = new PartitionKey(partitionKeyForCollection)
                                   });

        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteStoredProcedureAsync<TOutput, TPartitionKey>(string storedProcedureId,
                                                                                 TPartitionKey partitionKeyInCollection,
                                                                                 params dynamic[] procedureParams)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);

            return await documentClient.ExecuteStoredProcedureAsync<TOutput>(
                                UriFactory.CreateStoredProcedureUri(
                                    this.cosmosDbConnectionProperties.DatabaseName,
                                    this.cosmosDbConnectionProperties.CollectionName,
                                    storedProcedureId),
                                new RequestOptions
                                {
                                    PartitionKey = new PartitionKey(partitionKeyInCollection)
                                },
                                procedureParams);
        }

        /// <summary>
        /// Checks whether a document exists in Cosmos DB.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <returns>Returns true if a document with the specified ID exists in Cosmos DB.</returns>
        public async Task<bool> ExistsAsync(string documentId)
        {
            return await this.ExistsInPartitionAsync(documentId, null);
        }

        /// <summary>
        /// Checks if document Exists in Partition.
        /// </summary>
        /// <param name="documentId">Document Id.</param>
        /// <param name="partitionKeyInCollection">Partition Key.</param>
        /// <returns>Success/Failure.</returns>
        public async Task<bool> ExistsInPartitionAsync(string documentId, string partitionKeyInCollection)
        {
            await this.ReadAsync(documentId, partitionKeyInCollection);
            return true;
        }


        /// <summary>
        /// Reads a document in Cosmos DB as an asynchronous operation.
        /// </summary>
        /// <param name="documentId">Document ID.</param>
        /// <param name="partitionKeyInCollection">Partition key.</param>
        /// <returns>
        /// The JSON String based on the resourceKey.
        /// </returns>
        public async Task<JObject> ReadAsync(string documentId, string partitionKeyInCollection)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);

            Uri docLink = UriFactory.CreateDocumentUri(
                this.cosmosDbConnectionProperties.DatabaseName,
                this.cosmosDbConnectionProperties.CollectionName,
                documentId);

            ResourceResponse<Document> resourceResponse = null;

            if (string.IsNullOrWhiteSpace(partitionKeyInCollection))
            {
                resourceResponse = await documentClient.ReadDocumentAsync(docLink);
            }
            else
            {
                resourceResponse = await documentClient.ReadDocumentAsync(
                                       docLink,
                                       new RequestOptions
                                       {
                                           PartitionKey = new PartitionKey(partitionKeyInCollection)
                                       });

            }

            JObject responseDocument = (dynamic)resourceResponse?.Resource;
            return responseDocument;
        }

        /// <summary>
        /// Updates a document in Cosmos DB as an asynchronous operation.
        /// </summary>
        /// <param name="sourceDocumentId">Source document ID.</param>
        /// <param name="jsonObject">The document to be updated.</param>
        /// <param name="documentETag">Document ETag.</param>
        /// <returns>
        /// The resource returned in the Cosmos DB response.
        /// </returns>
        public async Task<string> ReplaceAsync(string sourceDocumentId, JObject jsonObject, string documentETag = null)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);
            Uri docLink = UriFactory.CreateDocumentUri(
                this.cosmosDbConnectionProperties.DatabaseName,
                this.cosmosDbConnectionProperties.CollectionName,
                sourceDocumentId);

            RequestOptions requestOptions = null;
            if (string.IsNullOrEmpty(documentETag) == false)
            {
                var accessCondition = new AccessCondition
                {
                    Condition = documentETag,
                    Type = AccessConditionType.IfMatch
                };

                requestOptions = new RequestOptions
                {
                    AccessCondition = accessCondition
                };
            }

            ResourceResponse<Document> resourceResponse = null;

            resourceResponse = await documentClient.ReplaceDocumentAsync(docLink, jsonObject, requestOptions);

            return resourceResponse?.Resource?.ToString();
        }
        /// <summary>
        /// Upserts a document in Cosmos DB as an asynchronous operation.
        /// </summary>
        /// <param name="jsonObject"> The document to be upserted.</param>
        /// <returns>
        /// The resource returned in the Cosmos DB response.
        /// </returns>
        public async Task<string> UpsertAsync(JObject jsonObject)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);
            ResourceResponse<Document> resourceResponse = null;
            resourceResponse = await documentClient.UpsertDocumentAsync(this.cosmosDbConnectionProperties.CollectionUri, jsonObject);
            return resourceResponse?.Resource?.ToString();
        }

        /// <inheritdoc />
        public async Task UpsertStoredProcedureAsync(string id, string body)
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);

            try
            {
                await documentClient.CreateStoredProcedureAsync(
                    this.cosmosDbConnectionProperties.CollectionUri,
                    new StoredProcedure
                    {
                        Id = id,
                        Body = body
                    });
            }
            catch (DocumentClientException dex) when (dex.StatusCode == HttpStatusCode.Conflict)
            {
                await documentClient.ReplaceStoredProcedureAsync(
                    UriFactory.CreateStoredProcedureUri(
                        this.cosmosDbConnectionProperties.DatabaseName,
                        this.cosmosDbConnectionProperties.CollectionName,
                        id),
                    new StoredProcedure
                    {
                        Id = id,
                        Body = body
                    });
            }
        }

        /// <summary>
        /// Gets the partition key from document collection.
        /// </summary>
        /// <returns></returns>
        private async Task GetPartitionKeyFromDocumentCollection()
        {
            DocumentClient documentClient = CosmosDbConnectionManager.Instance.GenerateConnection(this.cosmosDbConnectionProperties);
            ResourceResponse<DocumentCollection> documentCollection =
                await documentClient.ReadDocumentCollectionAsync(this.cosmosDbConnectionProperties.CollectionUri.OriginalString);
            // Cosmos DB does not allow more than 1 path in partition key
            this.partitionKey = documentCollection.Resource.PartitionKey.Paths.FirstOrDefault()?.Replace('/', '.');
        }

        /// <summary>
        /// Retry condition.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <returns><c>true</c>, retry; otherwise <c>false</c>.</returns>
        private bool RetryCondition(Exception exception)
        {
            switch (exception)
            {
                case DocumentClientException documentClientException:
                    return documentClientException.StatusCode.HasValue && RetriableHttpStatusCodes.Contains(documentClientException.StatusCode.Value);
                case HttpRequestException httpRequestException:
                    return true;
                default:
                    return false;
            }
        }
    }
}
