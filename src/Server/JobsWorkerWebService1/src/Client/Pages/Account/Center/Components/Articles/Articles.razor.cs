using JobsWorkerWebService.Client.Models;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace JobsWorkerWebService.Client.Pages.Account.Center
{
    public partial class Articles
    {
        [Parameter] public IList<ListItemDataType> List { get; set; }
    }
}