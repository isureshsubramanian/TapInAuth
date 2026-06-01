using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TapInAuth.Samples.SaaS.Pages;

[Authorize]
public class RecoveryModel : PageModel
{
    public void OnGet() { }
}
