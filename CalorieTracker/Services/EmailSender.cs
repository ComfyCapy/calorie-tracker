using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;

namespace CalorieTracker.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IResend _resend;
        private readonly IConfiguration _configuration;

        public EmailSender(IResend resend, IConfiguration configuration)
        {
            _resend = resend;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string email,
            string subject,
            string htmlMessage)
        {
            var message = new EmailMessage
            {
                From = _configuration["Resend:FromAddress"]
                    ?? "CalorieTracker <noreply@comfycapy.com>",
                To = email,
                Subject = subject,
                HtmlBody = htmlMessage
            };

            await _resend.EmailSendAsync(message);
        }
    }
}
