using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Description;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using HulloVMailManager.DataObjects;
using HulloVMailManager.Models;

namespace HulloVMailManager.Controllers
{
    public class VoicemailDTOController : TableController<VoicemailDTO>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            var context = new VoicemailDBContext();
            DomainManager = new EntityDomainManager<VoicemailDTO>(context, Request, Services);
        }

        // GET tables/VoicemailDTO
        public IQueryable<VoicemailDTO> GetAllVoicemailDTO()
        {
            return Query(); 
        }

        // GET tables/VoicemailDTO/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<VoicemailDTO> GetVoicemailDTO(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/VoicemailDTO/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task<VoicemailDTO> PatchVoicemailDTO(string id, Delta<VoicemailDTO> patch)
        {
             return UpdateAsync(id, patch);
        }

        // POST tables/VoicemailDTO/48D68C86-6EA6-4C25-AA33-223FC9A27959
        [ResponseType(typeof(VoicemailDTO))]
        public async Task<IHttpActionResult> PostVoicemailDTO(VoicemailDTO item)
        {
            VoicemailDTO current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/VoicemailDTO/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeleteVoicemailDTO(string id)
        {
             return DeleteAsync(id);
        }

    }
}