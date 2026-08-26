using Microsoft.AspNetCore.Identity.UI.Services;
using Resend;

namespace CalorieTracker.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IResend _resend;

        public EmailSender(IResend resend)
        {
            _resend = resend;
        }

        public async Task SendEmailAsync(
            string email,
            string subject,
            string htmlMessage)
        {
            var message = new EmailMessage
            {
                From = "CalorieTracker <noreply@comfycapy.com>",
                To = email,
                Subject = subject,
                HtmlBody = htmlMessage
            };

            await _resend.EmailSendAsync(message);
        }
    }
}