using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using HulloVMailMobileService.DataObjects;
using HulloVMailMobileService.Models;

namespace HulloVMailMobileService.Controllers
{
    public class HulloVMailController : TableController<HulloVMail>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            MobileServiceContext context = new MobileServiceContext();
            DomainManager = new EntityDomainManager<HulloVMail>(context, Request, Services);
        }

        // GET tables/HulloVMail
        public IQueryable<HulloVMail> GetAllHulloVMails()
        {
            return Query();
        }

        // GET tables/TodoItem/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<HulloVMail> GetHulloVMail(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/TodoItem/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task<HulloVMail> PatchHulloVMail(string id, Delta<HulloVMail> patch)
        {
            return UpdateAsync(id, patch);
        }

        // POST tables/TodoItem
        public async Task<IHttpActionResult> PostHulloVMail(HulloVMail item)
        {
            HulloVMail current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/TodoItem/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeleteHulloVMail(string id)
        {
            return DeleteAsync(id);
        }
    }
}