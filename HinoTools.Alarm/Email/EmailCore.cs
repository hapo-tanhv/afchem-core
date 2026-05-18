using System;
using System.Net;
using System.Net.Mail;

namespace HinoTools.Alarm.Email
{
    public class EmailCore
    {
        private readonly EmailParam param;

        private readonly SmtpClient smtpClient;
        
        public EmailCore(EmailParam param)
        {
            this.param = param;
            this.smtpClient = new SmtpClient
            {
                Host = param.Host,
                Port = param.Port,
                EnableSsl = param.EnableSSL,
                Timeout = param.TimeOut,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    param.CredentialEmail,
                    param.CredentialPassword)
            };
        }

        public void Send(EmailContent content)
        {
            try
            {
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.Subject = content.Subject;
                    mailMessage.From = new MailAddress(this.param.CredentialEmail);
                    mailMessage.Body = content.Body;
                    mailMessage.IsBodyHtml = true;
                    foreach (var recipient in content.Recipients)
                    {
                        mailMessage.To.Add(recipient);
                    }

                    this.smtpClient.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                return;
            }
        }
    }
}
