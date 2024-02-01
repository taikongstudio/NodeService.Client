using JobsWorkerWebService.Client.Models;
using JobsWorkerWebService.Client.Services;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace JobsWorkerWebService.Client.Pages.Profile
{
    public partial class Basic
    {
        private BasicProfileDataType _data = new BasicProfileDataType();

        [Inject] protected IProfileService ProfileService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            _data = await ProfileService.GetBasicAsync();
        }
    }
}