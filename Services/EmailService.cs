using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace WebDienThoai
{
    public static class EmailService
    {
        public static void Send(string toEmail, string subject, string body)
        {
            // Read keys matching Web.config: Smtp:Host, Smtp:Port, Smtp:User, Smtp:Pass, Smtp:From
            var host = ConfigurationManager.AppSettings["Smtp:Host"];
            var portStr = ConfigurationManager.AppSettings["Smtp:Port"];
            var user = ConfigurationManager.AppSettings["Smtp:User"];
            var pass = ConfigurationManager.AppSettings["Smtp:Pass"];
            var from = ConfigurationManager.AppSettings["Smtp:From"] ?? user;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                throw new InvalidOperationException("SMTP settings are missing in Web.config.");

            int port = 25;
            int.TryParse(portStr, out port);

            using (var client = new SmtpClient(host, port))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(user, pass);

                var msg = new MailMessage(from, toEmail, subject, body) { IsBodyHtml = false };
                client.Send(msg);
            }
        }
    }
}
