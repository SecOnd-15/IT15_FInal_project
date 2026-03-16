using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Latog_Final_project.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class SetPasswordConfirmationModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
