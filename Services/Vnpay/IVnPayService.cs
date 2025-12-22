using System.Collections.Specialized;
using System.Web;
using WebDienThoai.Models.Vnpay;

namespace WebDienThoai.Severvices.Vnpay
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
        PaymentResponseModel PaymentExecute(NameValueCollection collections);

    }
}
