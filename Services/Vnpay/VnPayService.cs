using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Web;
using WebDienThoai.Libraries;
using WebDienThoai.Models.Vnpay;
using WebDienThoai.Severvices.Vnpay;

namespace WebDienThoai.Services.Vnpay
{
    public class VnPayService : IVnPayService
    {
        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(ConfigurationManager.AppSettings["TimeZoneId"]);
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            var tick = !string.IsNullOrWhiteSpace(model.TxnRef)
            ? model.TxnRef
            : DateTime.Now.Ticks.ToString();

            var pay = new VnpayLibrary();

            var urlCallBack = ConfigurationManager.AppSettings["Vnpay:PaymentBackReturnUrl"];

            pay.AddRequestData("vnp_Version", ConfigurationManager.AppSettings["Vnpay:Version"]);
            pay.AddRequestData("vnp_Command", ConfigurationManager.AppSettings["Vnpay:Command"]);
            pay.AddRequestData("vnp_TmnCode", ConfigurationManager.AppSettings["Vnpay:TmnCode"]);
            pay.AddRequestData("vnp_Amount", ((int)model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", ConfigurationManager.AppSettings["Vnpay:CurrCode"]);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", ConfigurationManager.AppSettings["Vnpay:Locale"]);
            pay.AddRequestData("vnp_OrderInfo", $"{model.Name} {model.OrderDescription} {model.Amount}");
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", tick);

            var paymentUrl = pay.CreateRequestUrl(
                ConfigurationManager.AppSettings["Vnpay:BaseUrl"],
                ConfigurationManager.AppSettings["Vnpay:HashSecret"]
            );

            return paymentUrl;
        }

        public PaymentResponseModel PaymentExecute(NameValueCollection collections)
        {
            var pay = new VnpayLibrary();
            var rawQuery = HttpContext.Current?.Request?.Url?.Query; // rất quan trọng
            return pay.GetFullResponseData(collections, ConfigurationManager.AppSettings["Vnpay:HashSecret"], rawQuery);
        }
    }
}
