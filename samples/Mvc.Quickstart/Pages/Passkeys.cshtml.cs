using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TapInAuth.Samples.Mvc.Pages;

[Authorize]
public class PasskeysModel : PageModel
{
    public void OnGet() { }
}
