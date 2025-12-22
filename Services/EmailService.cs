using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace WebDienThoai
{
    public static class EmailService
    {
        public static void Send(string toEmail, string subject, string body, bool isHtml = false)
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

                var msg = new MailMessage(from, toEmail, subject, body) { IsBodyHtml = isHtml };
                client.Send(msg);
            }
        }

        public static void SendOtpEmail(string toEmail, string userName, string otp, int expiresMinutes = 10)
        {
            var subject = "OTP đặt lại mật khẩu";
            var safeUser = HttpUtility.HtmlEncode(userName ?? "");
            var body = $@"<!doctype html><html><head><meta charset='utf-8'>
<style>
  body{{font-family:'Segoe UI',Arial,sans-serif;background:#f7f7f8;color:#333}}
  .card{{max-width:560px;margin:24px auto;padding:24px;border:1px solid #eee;border-radius:12px;background:#fff}}
  .brand{{text-align:center;font-weight:700;letter-spacing:.5px;margin-bottom:8px;color:#222}}
  .title{{text-align:center;font-size:20px;font-weight:700;margin:0 0 12px;color:#222}}
  .desc{{text-align:center;font-size:14px;color:#666;margin-bottom:18px}}
  .otp{{display:inline-block;font-size:28px;font-weight:800;letter-spacing:6px;color:#222;background:#fafafa;border:1px dashed #d9d9d9;border-radius:8px;padding:12px 16px}}
  .hint{{font-size:12px;color:#888;margin-top:8px;text-align:center}}
  .footer{{font-size:12px;color:#999;text-align:center;margin-top:18px}}
</style></head>
<body>
  <div class='card'>
    <div class='brand'>Cửa Hàng Điện Tử Công Thương</div>
    <div class='title'>Xác thực đặt lại mật khẩu</div>
    <div class='desc'>Xin chào <strong>{safeUser}</strong>, vui lòng sử dụng mã OTP bên dưới để tiếp tục đặt lại mật khẩu.</div>
    <div style='text-align:center'><span class='otp'>{otp}</span></div>
    <div class='hint'>Mã OTP có hiệu lực trong {expiresMinutes} phút. Không chia sẻ mã này cho bất kỳ ai.</div>
    <div class='footer'>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</div>
  </div>
</body></html>";
            Send(toEmail, subject, body, isHtml: true);
        }
    }
}
