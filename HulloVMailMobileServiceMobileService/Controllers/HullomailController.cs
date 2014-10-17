using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using HulloVMailMobileServiceMobileService.DataObjects;
using HulloVMailMobileServiceMobileService.Models;

namespace HulloVMailMobileServiceMobileService.Controllers
{
    public class HullomailController : TableController<Hullomail>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            MobileServiceContext context = new MobileServiceContext();
            DomainManager = new EntityDomainManager<Hullomail>(context, Request, Services);
        }

        // GET tables/Hullomail
        public IQueryable<Hullomail> GetAllHullomail()
        {
            return Query(); 
        }

        // GET tables/Hullomail/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<Hullomail> GetHullomail(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/Hullomail/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task<Hullomail> PatchHullomail(string id, Delta<Hullomail> patch)
        {
             return UpdateAsync(id, patch);
        }

        // POST tables/Hullomail/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public async Task<IHttpActionResult> PostHullomail(Hullomail item)
        {
            Hullomail current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/Hullomail/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeleteHullomail(string id)
        {
             return DeleteAsync(id);
        }
    }
}