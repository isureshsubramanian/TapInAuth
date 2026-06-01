using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TapInAuth.UI.Pages.Account;

/// <summary>Sign-in page model.</summary>
public class SignInModel : PageModel
{
    /// <summary>The optional return URL passed via query string.</summary>
    public string? ReturnUrl { get; private set; }

    /// <summary>GET handler.</summary>
    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;
}
