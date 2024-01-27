using System.Collections.Generic;
using JobsWorkerWebService.Models;
using Microsoft.AspNetCore.Components;

namespace JobsWorkerWebService.Pages.Account.Center
{
    public partial class Articles
    {
        [Parameter] public IList<ListItemDataType> List { get; set; }
    }
}