using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebDienThoai.Libraries;
using WebDienThoai.Models;
using WebDienThoai.Models.Vnpay;

namespace WebDienThoai.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly string _connAdmin =
            ConfigurationManager.ConnectionStrings["Conn_Admin"].ConnectionString;

        private readonly string _connKhach =
            ConfigurationManager.ConnectionStrings["Conn_Khach"].ConnectionString;

        private const string PENDING_PREFIX = "VNPAY_PENDING_";

        // ===== Popup theo _ResultPopup =====
        private void SetPopupSuccess(string msg)
        {
            TempData["PopupType"] = "success";
            TempData["PopupMsg"] = msg;
            TempData["Success"] = msg;
        }

        private void SetPopupError(string msg)
        {
            TempData["PopupType"] = "error";
            TempData["PopupMsg"] = msg;
            TempData["Error"] = msg;
        }

        // ===== Helpers =====
        private string Cfg(string key) => ConfigurationManager.AppSettings[key];

        private int GetUserId()
        {
            try { return Convert.ToInt32(Session["UserId"] ?? 0); }
            catch { return 0; }
        }

        private string GetUserName() => (Session["UserName"] ?? "").ToString();

        private string GetHoTen() => (Session["HoTen"] ?? Session["TENKH"] ?? "").ToString();

        private int GetDefaultMaKho()
        {
            // nếu hệ bạn có kho mặc định (thường là 1)
            return 1;
        }

        private decimal GetGiaBan(int masp)
        {
            // lấy giá từ fn_SanPham_ChiTiet giống GioHangController đang dùng
            using (var conn = new SqlConnection(_connKhach))
            using (var cmd = new SqlCommand("SELECT GIABAN FROM dbo.fn_SanPham_ChiTiet(@MASP)", conn))
            {
                cmd.Parameters.Add("@MASP", SqlDbType.Int).Value = masp;
                conn.Open();
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value) return 0m;
                return Convert.ToDecimal(obj);
            }
        }

        // =========================
        // VNPAY cho GIỎ HÀNG (selectedItems)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePaymentUrlVnpay(PaymentInformationModel model, int[] selectedItems)
        {
            var tentk = GetUserName();
            var userId = GetUserId();

            if (userId <= 0 || string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi thanh toán.");
                return RedirectToAction("Index", "GioHang");
            }

            if (selectedItems == null || selectedItems.Length == 0)
            {
                SetPopupError("Bạn chưa chọn sản phẩm để thanh toán.");
                return RedirectToAction("Index", "GioHang");
            }

            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            if (!cart.Any())
            {
                SetPopupError("Giỏ hàng trống.");
                return RedirectToAction("Index", "GioHang");
            }

            var set = new HashSet<int>(selectedItems);
            var chosen = cart.Where(x => set.Contains(x.MASP)).ToList();
            if (!chosen.Any())
            {
                SetPopupError("Danh sách sản phẩm thanh toán không hợp lệ.");
                return RedirectToAction("Index", "GioHang");
            }

            decimal total = chosen.Sum(x => Convert.ToDecimal(x.DONGIA) * x.SOLUONG);
            if (total <= 0)
            {
                SetPopupError("Tổng thanh toán không hợp lệ.");
                return RedirectToAction("Index", "GioHang");
            }

            // TxnRef: dùng ticks để unique
            var txnRef = DateTime.Now.Ticks.ToString();

            // Lưu pending vào Session để callback có dữ liệu tạo hóa đơn (giống logic giỏ hàng)
            Session[PENDING_PREFIX + txnRef] = new PendingVnpayOrder
            {
                Flow = "CART",
                TxnRef = txnRef,
                UserName = tentk,
                UserId = userId,
                MaKho = GetDefaultMaKho(),
                CreatedAtUtc = DateTime.UtcNow,
                Items = chosen.Select(x => new PendingItem { MASP = x.MASP, SOLUONG = x.SOLUONG }).ToList()
            };

            var paymentUrl = BuildVnpayUrl(txnRef, total, model, System.Web.HttpContext.Current);
            return Redirect(paymentUrl);
        }

        // =========================
        // VNPAY cho MUA NGAY (1 SP)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePaymentUrlVnpayMuaNgay(int masp, int soluong = 1)
        {
            var tentk = GetUserName();
            var userId = GetUserId();

            if (userId <= 0 || string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi thanh toán.");
                return RedirectToAction("Index", "GioHang");
            }

            if (soluong < 1) soluong = 1;

            var gia = GetGiaBan(masp);
            if (gia <= 0)
            {
                SetPopupError("Không lấy được giá sản phẩm để thanh toán.");
                return RedirectToAction("Index", "GioHang");
            }

            decimal total = gia * soluong;
            var txnRef = DateTime.Now.Ticks.ToString();

            Session[PENDING_PREFIX + txnRef] = new PendingVnpayOrder
            {
                Flow = "BUY_NOW",
                TxnRef = txnRef,
                UserName = tentk,
                UserId = userId,
                MaKho = GetDefaultMaKho(),
                CreatedAtUtc = DateTime.UtcNow,
                Items = new List<PendingItem> { new PendingItem { MASP = masp, SOLUONG = soluong } }
            };

            // model cho VNPAY
            var payModel = new PaymentInformationModel
            {
                OrderType = "other",
                Name = string.IsNullOrWhiteSpace(GetHoTen()) ? tentk : GetHoTen(),
                OrderDescription = $"Mua ngay (1 sản phẩm)",
                Amount = (int)Math.Round(total, 0)
            };

            var paymentUrl = BuildVnpayUrl(txnRef, total, payModel, System.Web.HttpContext.Current);
            return Redirect(paymentUrl);
        }

        // =========================
        // CALLBACK VNPAY -> back giỏ hàng + popup
        // =========================
        [HttpGet]
        public ActionResult PaymentCallbackVnpay()
        {
            var hashSecret = Cfg("Vnpay:HashSecret");
            var rawQuery = Request?.Url?.Query; // rất quan trọng cho signature

            var pay = new VnpayLibrary();
            var response = pay.GetFullResponseData(Request.QueryString, hashSecret, rawQuery);

            // signature fail
            if (!response.Success)
            {
                SetPopupError("Thanh toán VNPAY thất bại: Sai chữ ký (signature).");
                return RedirectToAction("Index", "GioHang");
            }

            var txnRef = (response.OrderId ?? "").Trim(); // vnp_TxnRef
            if (string.IsNullOrWhiteSpace(txnRef))
            {
                SetPopupError("Thanh toán VNPAY thất bại: Không đọc được TxnRef.");
                return RedirectToAction("Index", "GioHang");
            }

            var key = PENDING_PREFIX + txnRef;
            var pending = Session[key] as PendingVnpayOrder;

            if (pending == null || pending.Items == null || pending.Items.Count == 0)
            {
                SetPopupError("Phiên thanh toán đã hết hạn hoặc không tìm thấy dữ liệu để ghi nhận đơn.");
                return RedirectToAction("Index", "GioHang");
            }

            // cancel/fail
            if (response.VnPayResponseCode != "00")
            {
                Session.Remove(key);
                SetPopupError($"Thanh toán VNPAY thất bại hoặc bị hủy. Mã phản hồi: {response.VnPayResponseCode}.");
                return RedirectToAction("Index", "GioHang");
            }

            // success -> tạo hóa đơn bằng đúng proc (trừ tồn + gán kho)
            try
            {
                var rs = ExecThanhToanGioHang(pending.UserName, pending.MaKho, pending.Items);

                // update HOADON theo constraint CK_HOADON_TINHTRANG
                UpdateHoaDonThanhCong(rs.MaHD, response.TransactionId);

                // nếu từ giỏ hàng -> xóa item khỏi Session giỏ (giống logic ThanhToan thường)
                if (string.Equals(pending.Flow, "CART", StringComparison.OrdinalIgnoreCase))
                {
                    RemovePurchasedItemsFromSessionCart(pending.Items.Select(i => i.MASP).ToList());
                }

                Session.Remove(key);

                SetPopupSuccess($"Thanh toán VNPAY thành công. Mã HĐ: {rs.MaHD} - Mã GD: {response.TransactionId}.");
                return RedirectToAction("Index", "GioHang");
            }
            catch (Exception ex)
            {
                Session.Remove(key);
                SetPopupError("Thanh toán VNPAY đã thành công nhưng ghi nhận đơn hàng thất bại: " + ex.Message);
                return RedirectToAction("Index", "GioHang");
            }
        }

        // =========================
        // Build VNPAY URL
        // =========================
        private string BuildVnpayUrl(string txnRef, decimal total, PaymentInformationModel model, HttpContext context)
        {
            var timeZoneId = Cfg("TimeZoneId");
            if (string.IsNullOrWhiteSpace(timeZoneId)) timeZoneId = "SE Asia Standard Time";

            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch { tz = TimeZoneInfo.Local; }

            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            var version = Cfg("Vnpay:Version");
            var command = Cfg("Vnpay:Command");
            var tmnCode = Cfg("Vnpay:TmnCode");
            var currCode = Cfg("Vnpay:CurrCode");
            var locale = Cfg("Vnpay:Locale");
            var baseUrl = Cfg("Vnpay:BaseUrl");
            var hashSecret = Cfg("Vnpay:HashSecret");
            var returnUrl = Cfg("Vnpay:PaymentBackReturnUrl");

            if (string.IsNullOrWhiteSpace(locale)) locale = "vn";
            if (string.IsNullOrWhiteSpace(currCode)) currCode = "VND";

            var buyerName = (model?.Name ?? GetHoTen() ?? "KhachHang").Trim();
            if (string.IsNullOrWhiteSpace(buyerName)) buyerName = "KhachHang";

            var desc = (model?.OrderDescription ?? "Thanh toan").Trim();
            if (string.IsNullOrWhiteSpace(desc)) desc = "Thanh toan";

            var orderInfo = $"{buyerName} {desc} {total:0}";

            var pay = new VnpayLibrary();

            pay.AddRequestData("vnp_Version", version);
            pay.AddRequestData("vnp_Command", command);
            pay.AddRequestData("vnp_TmnCode", tmnCode);

            pay.AddRequestData("vnp_Amount", ((long)(total * 100m)).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", currCode);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", locale);

            pay.AddRequestData("vnp_OrderInfo", orderInfo);
            pay.AddRequestData("vnp_OrderType", string.IsNullOrWhiteSpace(model?.OrderType) ? "other" : model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", returnUrl);

            // TxnRef để callback tìm pending
            pay.AddRequestData("vnp_TxnRef", txnRef);

            return pay.CreateRequestUrl(baseUrl, hashSecret);
        }

        // =========================
        // PROC: sp_ThanhToan_GioHang (trừ tồn + gán kho)
        // =========================
        private PaymentResult ExecThanhToanGioHang(string tentk, int maKho, List<PendingItem> items)
        {
            var tvp = new DataTable();
            tvp.Columns.Add("MASP", typeof(int));
            tvp.Columns.Add("SOLUONG", typeof(int));

            foreach (var it in items)
                if (it.SOLUONG > 0) tvp.Rows.Add(it.MASP, it.SOLUONG);

            if (tvp.Rows.Count == 0)
                throw new Exception("Danh sách sản phẩm không hợp lệ.");

            using (var conn = new SqlConnection(_connAdmin))
            using (var cmd = new SqlCommand("dbo.sp_ThanhToan_GioHang", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;

                // IMPORTANT: phải có kho cụ thể
                cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = maKho;

                var p = cmd.Parameters.AddWithValue("@Items", tvp);
                p.SqlDbType = SqlDbType.Structured;
                p.TypeName = "dbo.TT_GioHangItem";

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        int mahd = rd["MAHD"] != DBNull.Value ? Convert.ToInt32(rd["MAHD"]) : 0;
                        decimal thanhtien = rd["THANHTIEN"] != DBNull.Value ? Convert.ToDecimal(rd["THANHTIEN"]) : 0m;

                        if (mahd <= 0) throw new Exception("Không nhận được MAHD từ sp_ThanhToan_GioHang.");
                        return new PaymentResult { MaHD = mahd, ThanhTien = thanhtien };
                    }
                }
            }

            throw new Exception("sp_ThanhToan_GioHang không trả về kết quả.");
        }

        // =========================
        // UPDATE HOADON sau khi VNPAY OK
        // =========================
        private void UpdateHoaDonThanhCong(int mahd, string maGiaoDich)
        {
            using (var con = new SqlConnection(_connAdmin))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
UPDATE HOADON
SET TINHTRANG = @TINHTRANG,
    PHUONGTHUCTHANHTOAN = @PTTT,
    MAGIAODICH = @MAGIAODICH
WHERE MAHD = @MAHD;
", con))
                {
                    cmd.Parameters.Add("@MAHD", SqlDbType.Int).Value = mahd;
                    cmd.Parameters.Add("@TINHTRANG", SqlDbType.NVarChar, 50).Value = "Đã thanh toán";
                    cmd.Parameters.Add("@PTTT", SqlDbType.NVarChar, 50).Value = "VNPAY";
                    cmd.Parameters.Add("@MAGIAODICH", SqlDbType.NVarChar, 100).Value =
                        string.IsNullOrWhiteSpace(maGiaoDich) ? (object)DBNull.Value : maGiaoDich;

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void RemovePurchasedItemsFromSessionCart(List<int> masps)
        {
            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            if (!cart.Any() || masps == null || masps.Count == 0) return;

            var set = new HashSet<int>(masps);
            cart = cart.Where(x => !set.Contains(x.MASP)).ToList();
            Session["GioHang"] = cart;
        }

        [Serializable]
        private class PendingVnpayOrder
        {
            public string Flow { get; set; } // CART | BUY_NOW
            public string TxnRef { get; set; }
            public string UserName { get; set; }
            public int UserId { get; set; }
            public int MaKho { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public List<PendingItem> Items { get; set; }
        }

        [Serializable]
        private class PendingItem
        {
            public int MASP { get; set; }
            public int SOLUONG { get; set; }
        }

        private class PaymentResult
        {
            public int MaHD { get; set; }
            public decimal ThanhTien { get; set; }
        }
    }
}
