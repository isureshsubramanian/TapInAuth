using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TapInAuth.Samples.Mvc.Pages;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
