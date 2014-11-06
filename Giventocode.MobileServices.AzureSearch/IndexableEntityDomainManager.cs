using Microsoft.WindowsAzure.Mobile.Service;
using Microsoft.WindowsAzure.Mobile.Service.Tables;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.OData;

namespace Giventocode.MobileServices.AzureSearch
{
    public class IndexableEntityDomainManager<TData> : EntityDomainManager<TData> where TData : class, ITableData, new()
    {
        private IndexQueueManager IndexManager;

        public IndexableEntityDomainManager(DbContext context, HttpRequestMessage request, ApiServices services, string ServiceBusConnString)
            : base(context, request, services)
        {
            if (ServiceBusConnString == null)
            {
                throw new ArgumentNullException("ServiceBusConnString");
            }

            this.IndexManager = new IndexQueueManager(ServiceBusConnString);
        }
        public  override Task<TData> ReplaceAsync(string id, TData data)
        {
            return base.ReplaceAsync(id, data)
                .ContinueWith<TData>((t) => IndexManager.EnqueueEntity<TData>(t, "mergeOfUpload"));            
        }

        public  override Task<TData> UpdateAsync(string id, Delta<TData> patch)
        {
            return  base.UpdateAsync(id, patch)
                    .ContinueWith<TData>((t) => IndexManager.EnqueueEntity<TData>(t, "merge")); ;
        }

        public  override Task<TData> InsertAsync(TData data)
        {
            return base.InsertAsync(data)
                        .ContinueWith<TData>((t) =>IndexManager.EnqueueEntity<TData>(t, "upload"));            
        }

        public  override Task<bool> DeleteAsync(string id)
        {
            return base.DeleteAsync(id)
                .ContinueWith<bool>((t) => 
                    {
                        IndexManager.EnqueueEntity<TData>(Task<TData>.Run(() => new TData() { Id = id }), "delete");
                        return t.Result;
                    });
            
        }
    }
}