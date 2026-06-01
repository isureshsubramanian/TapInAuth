using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TapInAuth.Samples.Mvc.Pages;

[Authorize]
public class RecoveryModel : PageModel
{
    public void OnGet() { }
}
