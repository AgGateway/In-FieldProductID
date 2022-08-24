using Microsoft.Azure.Cosmos;

using System.Linq;
using System;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Collections.Generic;

namespace fxoagis.azure.io
{

  
    public class azureDocumentDB
    {
        private Action ResetConnection { get; set; }
        public DateTime ConnectionMadeDT { get; set; } = DateTime.MinValue;
        public CosmosClient client { get; private set; }
        public Container cn { get; private set; }
        
        private CancellationTokenSource cts { get; set; } = new CancellationTokenSource();

        public azureDocumentDB(CosmosClient client, string databaseId, string containerId)
        {
           

            this.client = client;
            cn = client.GetContainer(databaseId, containerId);

            ResetConnection = () => { return; };
            ConnectionMadeDT = DateTime.MaxValue;

        }
        public azureDocumentDB(string connectionString, string databaseId, string containerId)
        {
            ResetConnection = () =>
            {
                client = new CosmosClient(connectionString);
                cn = client.GetContainer(databaseId, containerId);
                ConnectionMadeDT = DateTime.UtcNow;
            };
            
            ResetConnection();
            

        }

        public static azureDocumentDB connectDB(string environConnectionParameterName, string databaseId, string containerId)
        {

            var cred = Environment.GetEnvironmentVariable(environConnectionParameterName, EnvironmentVariableTarget.Process);
            return new azureDocumentDB(cred, databaseId, containerId);



        }
        public azureDocumentDB connectDB(string databaseId, string containerId)
        {
            return new azureDocumentDB(client, databaseId, containerId);
        }

        public void ResetIfExpired()
        {
            var exp = DateTime.UtcNow - TimeSpan.FromHours(1);
            
            if(exp > ConnectionMadeDT)             
                ResetConnection?.Invoke();
             
        }

        public void CancelCurrentOperations()
        {
            cts.Cancel();
            cts = new CancellationTokenSource();
        }

        private JObject toDocument<T>(T obj, string id)
        {
            var jobj = JObject.FromObject(obj);
            jobj.Add(new JProperty("id", id));
            return jobj;

        }

        public async Task<HttpStatusCode> createDocument<T>(T data, string id)
        {
            var rsp = await cn.CreateItemAsync(toDocument(data, id), cancellationToken: cts.Token);
            return rsp.StatusCode;
        }
        public async Task<HttpStatusCode> createDocument<T>(T data)
        {
            var rsp = await cn.CreateItemAsync(data,cancellationToken: cts.Token);
            return rsp.StatusCode;
        }
        public async Task<HttpStatusCode> writeDocument<T>(T data, string id)
        {
            try
            {
                var rsp = await cn.UpsertItemAsync(toDocument(data,id), cancellationToken: cts.Token);
                return rsp.StatusCode;
            }
            catch
            {

                return HttpStatusCode.BadRequest;
            }
        }
        public async Task<HttpStatusCode> writeDocument<T>(T data)
        {
            try
            {
                var rsp = await cn.UpsertItemAsync(data,cancellationToken: cts.Token);
                return rsp.StatusCode;
            }
            catch
            {
               
                return HttpStatusCode.BadRequest;
            }
        }
        public async Task<T> readDocument<T>(string id, string partition)
        { 
            var rsp = await cn.ReadItemAsync<T>(id, new PartitionKey(partition), null, cts.Token);
            return rsp.Resource;
        }
        public async Task<List<T>> queryDocuments<T>(string query)
        {
            var list = new List<T>();
            var qDef = new QueryDefinition(query);
            var qOpt = new QueryRequestOptions { MaxConcurrency = -1, MaxBufferedItemCount = -1 };

          
            using var it = cn.GetItemQueryIterator<T>(qDef, null, qOpt);
            

                while (it.HasMoreResults)
                {
                    var rsp = await it.ReadNextAsync(cts.Token);
                    switch (rsp.StatusCode)
                    {
                        case HttpStatusCode.Forbidden:
                            break;
                        case HttpStatusCode.TooManyRequests:
                            break;

                        default:
                            list.AddRange(rsp.Resource);
                            break;
                    }

                    if (cts.IsCancellationRequested)
                        break;
                }

            return list;
        }
        public async Task<HttpStatusCode> deleteDocument<T>(string id,string partition)
        {
            var rsp = await cn.DeleteItemAsync<T>(id, new PartitionKey(partition));
            return rsp.StatusCode;
        }
    }

  
}
