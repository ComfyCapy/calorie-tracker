using System.Text;
using System.Text.Encodings.Web;
using CalorieTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace CalorieTracker.Services;

// Called only after the account (and external login, when applicable) exists.
// Identity outcomes, role assignment and redirects remain in the entry pages.
public sealed class AccountOnboardingService(
    UserManager<ApplicationUser> users,
    CapyProvisioningService provisioning,
    IEmailSender emailSender)
{
    public async Task ProvisionAndConfirmAsync(ApplicationUser user, string userId, string email,
        Func<string, string, string> confirmationUrl)
    {
        await provisioning.ProvisionAsync(userId);

        var code = await users.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = confirmationUrl(userId, code);

        await emailSender.SendEmailAsync(
            email,
            "Confirm your Comfy Capy Calories account",
            $"""
            <p>Heya!</p>

            <p>This is Comfy Capy handing you your very own confirmation link!</p>

            <p>
                <a href="{HtmlEncoder.Default.Encode(callbackUrl)}">
                    Confirm your email address
                </a>
            </p>

            <p>
                Feel free to give that link a click to confirm your account and finish setting everything up.
            </p>

            <p>We hope to see you soon~!</p>

            <p>~ Comfy Capy</p>
            """);

    }
}
