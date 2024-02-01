using JobsWorkerWebService.Client.Models;
using JobsWorkerWebService.Client.Services;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace JobsWorkerWebService.Client.Pages.Account.Settings.Components
{
    public partial class BaseView
    {
        private CurrentUser _currentUser = new CurrentUser();

        [Inject] protected IUserService UserService { get; set; }

        private void HandleFinish()
        {
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            _currentUser = await UserService.GetCurrentUserAsync();
        }
    }
}