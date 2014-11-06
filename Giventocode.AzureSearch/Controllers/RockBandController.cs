using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using Giventocode.AzureSearch.DataObjects;
using Giventocode.AzureSearch.Models;
using Giventocode.MobileServices.AzureSearch;
using Microsoft.WindowsAzure;

namespace Giventocode.AzureSearch.Controllers
{
    public class RockBandController : TableController<RockBand>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            MobileServiceContext context = new MobileServiceContext();
            DomainManager = new IndexableEntityDomainManager<RockBand>(context, Request, Services, CloudConfigurationManager.GetSetting("Search_ServiceBusConnString"));
        }

        // GET tables/RockBand
        public IQueryable<RockBand> GetAllRockBand()
        {
            return Query(); 
        }

        // GET tables/RockBand/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<RockBand> GetRockBand(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/RockBand/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task<RockBand> PatchRockBand(string id, Delta<RockBand> patch)
        {
             return UpdateAsync(id, patch);
        }

        // POST tables/RockBand/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public async Task<IHttpActionResult> PostRockBand(RockBand item)
        {
            RockBand current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/RockBand/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeleteRockBand(string id)
        {
             return DeleteAsync(id);
        }

    }
}