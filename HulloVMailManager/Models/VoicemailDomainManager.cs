using AutoMapper;
using HulloVMailManager.DataObjects;
using Microsoft.WindowsAzure.Mobile.Service;
using HulloVMailManager.DataObjects;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;

namespace HulloVMailManager.Models
{
    public class VoicemailDomainManager : MappedEntityDomainManager<VoicemailDTO, Voicemail>
    {
        private VoicemailDBContext context;

        public VoicemailDomainManager(VoicemailDBContext context, HttpRequestMessage request, ApiServices services)
            : base(context, request, services)
        {
            Request = request;
            this.context = context;
        }

        public static int GetKey(string VoicemailDTOId, DbSet<Voicemail> voicemails, HttpRequestMessage request)
        {
            int voicemailId = voicemails
               .Where(v => v.Id == VoicemailDTOId)
               .Select(v => v.VoicemailID)
               .FirstOrDefault();

            if (voicemailId == 0)
            {
                throw new HttpResponseException(request.CreateNotFoundResponse());
            }
            return voicemailId;
        }

        protected override T GetKey<T>(string voicemailDTOId)
        {
            return (T)(object)GetKey(voicemailDTOId, this.context.Voicemails, this.Request);
        }

        public override SingleResult<VoicemailDTO> Lookup(string voicemailDTOId)
        {
            int voicemailId = GetKey<int>(voicemailDTOId);
            return LookupEntity(v => v.VoicemailID == voicemailId);
        }

        public override async Task<VoicemailDTO> InsertAsync(VoicemailDTO voicemailDto)
        {
            return await base.InsertAsync(voicemailDto);
        }

        public override async Task<VoicemailDTO> UpdateAsync(string voicemailDTOId, Delta<VoicemailDTO> patch)
        {
            int voicemailId = GetKey<int>(voicemailDTOId);

            Voicemail existingVoicemail = await this.Context.Set<Voicemail>().FindAsync(voicemailId);
            if (existingVoicemail == null)
            {
                throw new HttpResponseException(this.Request.CreateNotFoundResponse());
            }

            VoicemailDTO existingVoicemailDTO = Mapper.Map<Voicemail, VoicemailDTO>(existingVoicemail);
            patch.Patch(existingVoicemailDTO);
            Mapper.Map<VoicemailDTO, Voicemail>(existingVoicemailDTO, existingVoicemail);

            await this.SubmitChangesAsync();

            VoicemailDTO updatedCustomerDTO = Mapper.Map<Voicemail, VoicemailDTO>(existingVoicemail);

            return updatedCustomerDTO;
        }

        public override async Task<VoicemailDTO> ReplaceAsync(string voicemailDtoId, VoicemailDTO voicemailDto)
        {
            return await base.ReplaceAsync(voicemailDtoId, voicemailDto);
        }

        public override async Task<bool> DeleteAsync(string voicemailDtoId)
        {
            int voicemailId = GetKey<int>(voicemailDtoId);
            return await DeleteItemAsync(voicemailId);
        }
    }
}