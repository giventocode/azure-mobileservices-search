using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.WindowsAzure.Mobile.Service;
using Giventocode.MobileServices.AzureSearch;
using Microsoft.WindowsAzure;

namespace Giventocode.AzureSearch.ScheduledJobs
{
    // A simple scheduled job which can be invoked manually by submitting an HTTP
    // POST request to the path "/jobs/sample".

    public class IndexerJob : ScheduledJob
    {
        public async override Task ExecuteAsync()
        {
            var mgr = new IndexQueueManager(CloudConfigurationManager.GetSetting("Search_ServiceBusConnString"));
            var maxNumberOfMgs = 1000;
            var processMsgs=0;

            while (await mgr.ReadFromQueueAndIndexAsync() && processMsgs < maxNumberOfMgs)
            {
                processMsgs++;
                Services.Log.Info(string.Format("Processed message. Message count {0}",processMsgs.ToString()));
                
            }
                     
        }
    }
}