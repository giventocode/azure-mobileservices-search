using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Mobile.Service.Tables;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Giventocode.MobileServices.AzureSearch
{
    public class IndexQueueManager
    {

        private QueueClient _indexerQ;

        private Uri _indexUri;

        private Uri IndexUri
        {
            get
            {
                if (_indexUri == null)
                {
                    _indexUri = new Uri(string.Format("https://{0}.search.windows.net/indexes/{1}?api-version=2014-07-31-Preview", CloudConfigurationManager.GetSetting("Search_ServiceName"), CloudConfigurationManager.GetSetting("Search_IndexName")));
                }
                return _indexUri;
            }
        }

        private Uri _indexPostUri;

        private Uri IndexPostUri
        {
            get
            {
                if (_indexPostUri == null)
                {
                    _indexPostUri = new Uri(string.Format("https://{0}.search.windows.net/indexes?api-version=2014-07-31-Preview", CloudConfigurationManager.GetSetting("Search_ServiceName")));
                }
                return _indexPostUri;
            }
        }

        private Uri _docsOpsUri;

        private Uri DocsOpsUri
        {
            get
            {
                if (_docsOpsUri == null)
                {
                    _docsOpsUri = new Uri(string.Format("https://{0}.search.windows.net/indexes/{1}/docs/index?api-version=2014-07-31-Preview", CloudConfigurationManager.GetSetting("Search_ServiceName"), CloudConfigurationManager.GetSetting("Search_IndexName")));
                }
                return _docsOpsUri;
            }
        }


        public IndexQueueManager(string connString)
        {
            if (connString == null)
            {
                throw new ArgumentNullException("connString");
            }            

            var mgr = NamespaceManager.CreateFromConnectionString(connString);

            if (!mgr.QueueExists("amsindexerqueue"))
            {                
                var q = mgr.CreateQueue("amsindexerqueue");              
            }

            _indexerQ = QueueClient.CreateFromConnectionString(connString,"amsindexerqueue");
            
        }

        public TData EnqueueEntity<TData>(Task<TData> tEntity, string action) where TData : class, ITableData
        {

            if (tEntity.IsFaulted)
            {
                throw new InvalidOperationException("Task in faulted state", tEntity.Exception);
            }
            
            var ixEntity = GetEntityIndexInfo<TData>(tEntity.Result, action);
            
            var msg = new BrokeredMessage(JsonConvert.SerializeObject(ixEntity));
            msg.TimeToLive = new TimeSpan(7, 0, 0, 0);
            _indexerQ.Send(msg);

            return tEntity.Result;
        }


        private EntityIndexInfo GetEntityIndexInfo<TData>(TData entity, string action) where TData : class, ITableData
        {
            var ixEntity = new EntityIndexInfo() { Action = action };
            var props = typeof(TData).GetProperties();

            foreach (var prop in props)
            {
                var att = prop.GetCustomAttribute<IndexableAttribute>();

                if (att != null)
                {
                    var name = att.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = prop.Name;
                    }

                    var ixAtt = new EntityPropertyIndexInfo() { Name = name.ToLower(), Value = prop.GetValue(entity), IndexDefinition = att };

                    ixAtt.IndexAttributes.Add("name", name.ToLower());
                    ixAtt.IndexAttributes.Add("type", att.Type);
                    ixAtt.IndexAttributes.Add("facetable", att.Facetable);
                    ixAtt.IndexAttributes.Add("filterable", att.Filterable);
                    ixAtt.IndexAttributes.Add("searchable", att.Searchable);
                    ixAtt.IndexAttributes.Add("sortable", att.Sortable);
                    ixAtt.IndexAttributes.Add("suggestions", att.Suggestions);
                    ixAtt.IndexAttributes.Add("retrievable", att.Retrievable);

                    ixEntity.Properties.Add(ixAtt);
                }

            }

            var ixKeyProp = new EntityPropertyIndexInfo() { Name = "id", Value = entity.Id };

            ixKeyProp.IndexAttributes.Add("name", "id");
            ixKeyProp.IndexAttributes.Add("type", "Edm.String");
            ixKeyProp.IndexAttributes.Add("key", true);

            ixEntity.Properties.Add(ixKeyProp);

            return ixEntity;
        }

        private EntityIndexInfo GetEntity()
        {

            var msg = _indexerQ.Receive();

            if (msg == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<EntityIndexInfo>(msg.GetBody<string>());

        }

        public async Task<Boolean> ReadFromQueueAndIndexAsync()
        {
            try
            {
                var msg = _indexerQ.Receive();

                if (msg == null)
                {
                    return false;
                }

                var indexInfo =  JsonConvert.DeserializeObject<EntityIndexInfo>(msg.GetBody<string>());

                await CreateIndexIfNotExistsAsync(indexInfo);

                var indexDoc = GetIndexDocument(indexInfo);
                
                await IndexDocumentAsync(indexDoc);

                _indexerQ.Complete(msg.LockToken);

                return true;
            }
            catch(Exception ex)
            {
                _indexerQ.Abort();

                throw ex;
            }

        }

        private IndexDocument GetIndexDocument(EntityIndexInfo indexInfo)
        {
            var indexDoc =new IndexDocument();
            var dic = new Dictionary<string, object>();

            dic.Add("@search.action", indexInfo.Action);

            var keyPropName = indexInfo.GetKeyPropertyName();

            foreach (var prop in indexInfo.Properties)
            {
                if (indexInfo.Action != "delete" || prop.Name == keyPropName)
                {
                    dic.Add(prop.Name, prop.Value);
                }
            };
            
            indexDoc.Values.Add(dic);

            return indexDoc;
        }

        private async Task IndexDocumentAsync(IndexDocument doc)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("api-key", CloudConfigurationManager.GetSetting("Search_Key"));

                var m = new HttpRequestMessage(HttpMethod.Post, this.DocsOpsUri);
                m.Content = new StringContent(JsonConvert.SerializeObject(doc));
                m.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await client.SendAsync(m);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(string.Format("Failed to perform the index update for the document. Response reason phrase {0}", response.ReasonPhrase));
                }

            }

        }

        private async Task CreateIndexIfNotExistsAsync(EntityIndexInfo indexSchema)
        {

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("api-key", CloudConfigurationManager.GetSetting("Search_Key"));


                var response = await client.GetAsync(this.IndexUri);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var indexModel = new IndexModel()
                    {
                        Name = CloudConfigurationManager.GetSetting("Search_IndexName"),
                        Fields = indexSchema.Properties
                                .Select<EntityPropertyIndexInfo, Dictionary<string, object>>(p => p.IndexAttributes)
                                .ToList<Dictionary<string, object>>()
                    };
               

                    var m = new HttpRequestMessage(HttpMethod.Post, this.IndexPostUri);
                    m.Content = new StringContent(JsonConvert.SerializeObject(indexModel));
                    m.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    response = await client.SendAsync(m);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(string.Format("Failed to created the index {0}", response.ReasonPhrase));
                    }

                }
            }

        }
    }
}