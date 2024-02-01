using JobsWorkerWebService.Models;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace JobsWorkerWebService.Pages.Account.Center
{
    public partial class Articles
    {
        [Parameter] public IList<ListItemDataType> List { get; set; }
    }
}