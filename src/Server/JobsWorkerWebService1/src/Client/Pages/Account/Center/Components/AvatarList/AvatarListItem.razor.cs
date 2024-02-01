using AntDesign;
using Microsoft.AspNetCore.Components;

namespace JobsWorkerWebService.Client.Pages.Account.Center.Components.AvatarList
{
    public partial class AvatarListItem
    {
        [Parameter] public string Size { get; set; }
        [Parameter] public string Tips { get; set; }
        [Parameter] public string Src { get; set; }
        [Parameter] public EventCallback OnClick { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            SetClassMap();
        }

        protected void SetClassMap()
        {
            ClassMapper
                .Clear()
                .Add("avatarItem")
                .If("avatarItemLarge", () => Size == "large")
                .If("avatarItemSmall", () => Size == "small")
                .If("avatarItemMini", () => Size == "mini");
        }
    }
}