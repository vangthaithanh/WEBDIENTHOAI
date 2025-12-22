using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebDienThoai.Models;
using WebDienThoai.Models.ViewModels;

namespace WebDienThoai.Controllers
{
    public class GioHangController : Controller
    {
        // Đọc sản phẩm/chi tiết: có thể dùng Conn_Khach
        private readonly string _connKhach =
            ConfigurationManager.ConnectionStrings["Conn_Khach"].ConnectionString;

        // THANH TOÁN: dùng Conn_Admin y như AccountController.Login
        private readonly string _connAdmin =
            ConfigurationManager.ConnectionStrings["Conn_Admin"].ConnectionString;

        private void SetPopupSuccess(string msg)
        {
            TempData["PopupType"] = "success";
            TempData["PopupMsg"] = msg;
            TempData["Success"] = msg; // giữ tương thích view cũ
        }

        private void SetPopupError(string msg)
        {
            TempData["PopupType"] = "error";
            TempData["PopupMsg"] = msg;
            TempData["Error"] = msg; // giữ tương thích view cũ
        }

        public ActionResult Index()
        {
            ViewBag.BreadcrumbList = new List<WebDienThoai.Models.BreadcrumbItem>
            {
                new WebDienThoai.Models.BreadcrumbItem { Text = "Trang chủ", Action = "Index", Controller = "Home" },
                new WebDienThoai.Models.BreadcrumbItem { Text = "Giỏ hàng" }
            };
            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            ViewBag.TongSanPham = cart.Sum(i => i.SOLUONG);
            ViewBag.TongTien = cart.Sum(i => i.ThanhTien);
            return View(cart);
        }

        public ActionResult Add(int id, int qty = 1)
        {
            if (qty < 1) qty = 1;

            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            var existing = cart.FirstOrDefault(x => x.MASP == id);

            if (existing != null)
            {
                existing.SOLUONG += qty;
            }
            else
            {
                var p = GetProductDetail(id);
                if (p == null) return HttpNotFound();

                cart.Add(new GioHang
                {
                    MASP = p.MASP,
                    TENSP = p.TENSP,
                    DONGIA = p.GIABAN,
                    ANH = p.ANH,
                    SOLUONG = qty
                });
            }

            Session["GioHang"] = cart;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Update(int masp, int soluong)
        {
            var cart = Session["GioHang"] as List<GioHang>;
            if (cart != null)
            {
                var item = cart.FirstOrDefault(x => x.MASP == masp);
                if (item != null)
                {
                    if (soluong <= 0) cart.Remove(item);
                    else item.SOLUONG = soluong;

                    Session["GioHang"] = cart;
                }
            }
            return RedirectToAction("Index");
        }

        public ActionResult Remove(int id)
        {
            var cart = Session["GioHang"] as List<GioHang>;
            if (cart != null)
            {
                var itm = cart.FirstOrDefault(x => x.MASP == id);
                if (itm != null)
                {
                    cart.Remove(itm);
                    Session["GioHang"] = cart;
                }
            }
            return RedirectToAction("Index");
        }

        public ActionResult Clear()
        {
            Session.Remove("GioHang");
            return RedirectToAction("Index");
        }

        // =========================
        // MUA NGAY (dbo.sp_MuaNgay) - Conn_Admin
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MuaNgay(int masp, int soluong = 1)
        {
            var tentk = Session["UserName"] as string; // đúng theo AccountController
            if (string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi mua ngay.");
                //return RedirectToAction("Login", "Account");
            }

            if (soluong < 1) soluong = 1;

            try
            {
                int? mahd = null;
                decimal? thanhtien = null;

                using (var conn = new SqlConnection(_connAdmin))
                using (var cmd = new SqlCommand("dbo.sp_MuaNgay", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;
                    cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = DBNull.Value; // hoặc MAKHO cụ thể
                    cmd.Parameters.Add("@MASP", SqlDbType.Int).Value = masp;
                    cmd.Parameters.Add("@SOLUONG", SqlDbType.Int).Value = soluong;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            if (rd["MAHD"] != DBNull.Value) mahd = Convert.ToInt32(rd["MAHD"]);
                            if (rd["THANHTIEN"] != DBNull.Value) thanhtien = Convert.ToDecimal(rd["THANHTIEN"]);
                        }
                    }
                }

                SetPopupSuccess(mahd.HasValue
                    ? $"Mua ngay thành công. Mã HĐ: {mahd} - Tổng tiền: {(thanhtien ?? 0):N0} đ"
                    : "Mua ngay thành công.");
            }
            catch (SqlException ex)
            {
                // Nếu không đủ tồn / lỗi validate trong proc => sẽ rơi vào đây
                SetPopupError("Mua ngay thất bại: " + ex.Message);
            }
            catch (Exception ex)
            {
                SetPopupError("Mua ngay thất bại: " + ex.Message);
            }

            // Đang để về giỏ hàng để bạn thấy thông báo (popup/alert)
            return RedirectToAction("Index", "GioHang");
            // Nếu bạn muốn hiện ngay tại trang chi tiết sản phẩm:
            // return RedirectToAction("ChiTietSanPham", "SanPham", new { id = masp });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThanhToanMuaNgay(int masp, int soluong = 1)
        {
            var tentk = Session["UserName"] as string; 
            if (string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi mua ngay.");
                //return RedirectToAction("Login", "Account");
            }

            if (soluong < 1) soluong = 1;

            try
            {
                int? mahd = null;
                decimal? thanhtien = null;

                using (var conn = new SqlConnection(_connAdmin))
                using (var cmd = new SqlCommand("dbo.sp_MuaNgay", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;
                    cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = DBNull.Value; // hoặc MAKHO cụ thể
                    cmd.Parameters.Add("@MASP", SqlDbType.Int).Value = masp;
                    cmd.Parameters.Add("@SOLUONG", SqlDbType.Int).Value = soluong;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            if (rd["MAHD"] != DBNull.Value) mahd = Convert.ToInt32(rd["MAHD"]);
                            if (rd["THANHTIEN"] != DBNull.Value) thanhtien = Convert.ToDecimal(rd["THANHTIEN"]);
                        }
                    }
                }

                SetPopupSuccess(mahd.HasValue
                    ? $"Mua ngay thành công. Mã HĐ: {mahd} - Tổng tiền: {(thanhtien ?? 0):N0} đ"
                    : "Mua ngay thành công.");
            }
            catch (SqlException ex)
            {
                // Nếu không đủ tồn / lỗi validate trong proc => sẽ rơi vào đây
                SetPopupError("Mua ngay thất bại: " + ex.Message);
            }
            catch (Exception ex)
            {
                SetPopupError("Mua ngay thất bại: " + ex.Message);
            }

            return RedirectToAction("Index", "Home");
        }

        // =========================
        // THANH TOÁN GIỎ HÀNG (dbo.sp_ThanhToan_GioHang) - Conn_Admin
        // =========================
        public ActionResult XacNhanThanhToan(int[] selectedItems)
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi thanh toán.");
                return RedirectToAction("Login", "Account");
            }

            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            if (!cart.Any())
            {
                SetPopupError("Giỏ hàng trống.");
                return RedirectToAction("Index");
            }

            if (selectedItems == null || selectedItems.Length == 0)
            {
                SetPopupError("Vui lòng chọn sản phẩm để thanh toán.");
                return RedirectToAction("Index");
            }

            var picked = cart.Where(x => selectedItems.Contains(x.MASP)).ToList();
            if (!picked.Any())
            {
                SetPopupError("Danh sách sản phẩm thanh toán không hợp lệ.");
                return RedirectToAction("Index");
            }

            var vm = new CheckoutPreviewVM { Items = picked };
            return View(vm); // Views/GioHang/XacNhanThanhToan.cshtml
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanMuaNgay(int masp, int soluong = 1)
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi mua ngay.");
                return RedirectToAction("Login", "Account");
            }

            if (soluong < 1) soluong = 1;

            try
            {
                int? mahd = null;
                decimal? thanhtien = null;

                using (var conn = new SqlConnection(_connAdmin))
                using (var cmd = new SqlCommand("dbo.sp_MuaNgay", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;

                    // IMPORTANT: có kho cụ thể
                    cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = 1;

                    cmd.Parameters.Add("@MASP", SqlDbType.Int).Value = masp;
                    cmd.Parameters.Add("@SOLUONG", SqlDbType.Int).Value = soluong;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            if (rd["MAHD"] != DBNull.Value) mahd = Convert.ToInt32(rd["MAHD"]);
                            if (rd["THANHTIEN"] != DBNull.Value) thanhtien = Convert.ToDecimal(rd["THANHTIEN"]);
                        }
                    }
                }

                SetPopupSuccess(mahd.HasValue
                    ? $"Mua ngay thành công. Mã HĐ: {mahd} - Tổng tiền: {(thanhtien ?? 0):N0} đ"
                    : "Mua ngay thành công.");

                // back giỏ hàng để hiện popup giống flow khác
                return RedirectToAction("Index", "GioHang");
            }
            catch (SqlException ex)
            {
                SetPopupError("Mua ngay thất bại: " + ex.Message);
                return RedirectToAction("Index", "GioHang");
            }
            catch (Exception ex)
            {
                SetPopupError("Mua ngay thất bại: " + ex.Message);
                return RedirectToAction("Index", "GioHang");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThanhToan(int[] selectedItems)
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
            {
                SetPopupError("Bạn cần đăng nhập trước khi thanh toán.");
                return RedirectToAction("Login", "Account");
            }

            var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
            if (!cart.Any())
            {
                SetPopupError("Giỏ hàng trống.");
                return RedirectToAction("Index");
            }

            if (selectedItems == null || selectedItems.Length == 0)
            {
                SetPopupError("Bạn chưa chọn sản phẩm để thanh toán.");
                return RedirectToAction("Index");
            }

            var chosen = cart.Where(x => selectedItems.Contains(x.MASP)).ToList();
            if (!chosen.Any())
            {
                SetPopupError("Danh sách sản phẩm được chọn không hợp lệ.");
                return RedirectToAction("Index");
            }

            var tvp = new DataTable();
            tvp.Columns.Add("MASP", typeof(int));
            tvp.Columns.Add("SOLUONG", typeof(int));

            foreach (var it in chosen)
                if (it.SOLUONG > 0) tvp.Rows.Add(it.MASP, it.SOLUONG);

            if (tvp.Rows.Count == 0)
            {
                SetPopupError("Số lượng không hợp lệ.");
                return RedirectToAction("Index");
            }

            try
            {
                int? mahd = null;
                decimal? thanhtien = null;

                using (var conn = new SqlConnection(_connAdmin))
                using (var cmd = new SqlCommand("dbo.sp_ThanhToan_GioHang", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;
                    cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = DBNull.Value;

                    var p = cmd.Parameters.AddWithValue("@Items", tvp);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "dbo.TT_GioHangItem";

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            if (rd["MAHD"] != DBNull.Value) mahd = Convert.ToInt32(rd["MAHD"]);
                            if (rd["THANHTIEN"] != DBNull.Value) thanhtien = Convert.ToDecimal(rd["THANHTIEN"]);
                        }
                    }
                }

                // Xóa khỏi session chỉ những item đã thanh toán, giữ lại item chưa tick
                cart = cart.Where(x => !selectedItems.Contains(x.MASP)).ToList();
                Session["GioHang"] = cart;

                SetPopupSuccess(mahd.HasValue
                    ? $"Thanh toán thành công. Mã HĐ: {mahd} - Tổng tiền: {(thanhtien ?? 0):N0} đ"
                    : "Thanh toán thành công.");
            }
            catch (SqlException ex)
            {
                SetPopupError("Thanh toán thất bại: " + ex.Message);
            }
            catch (Exception ex)
            {
                SetPopupError("Thanh toán thất bại: " + ex.Message);
            }

            return RedirectToAction("Index");
        }

        private XemChiTietModel GetProductDetail(int id)
        {
            XemChiTietModel result = null;

            using (var conn = new SqlConnection(_connKhach))
            using (var cmd = new SqlCommand("SELECT * FROM dbo.fn_SanPham_ChiTiet(@MASP)", conn))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@MASP", id);

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        result = new XemChiTietModel
                        {
                            MASP = (int)rd["MASP"],
                            TENSP = rd["TENSP"].ToString(),
                            GIABAN = rd["GIABAN"] != DBNull.Value ? (decimal)rd["GIABAN"] : 0,
                            ANH = rd["ANH"] == DBNull.Value ? null : rd["ANH"].ToString()
                        };
                    }
                }
            }
            return result;
        }
        //ưu đãi
        //public ActionResult UuDaiGioHang()
        //{
        //    var cart = Session["GioHang"] as List<GioHang> ?? new List<GioHang>();
        //    int tongSL = cart.Sum(x => x.SOLUONG);

        //    List<UuDai> list = new List<UuDai>();

        //    using (var conn = new SqlConnection(_connKhach))
        //    using (var cmd = new SqlCommand("PROC_UUDAI_GIOHANG", conn))
        //    {
        //        cmd.CommandType = CommandType.StoredProcedure;
        //        cmd.Parameters.AddWithValue("@TongSoLuong", tongSL);

        //        conn.Open();
        //        using (var rd = cmd.ExecuteReader())
        //        {
        //            while (rd.Read())
        //            {
        //                list.Add(new UuDai
        //                {
        //                    MaPKM = (int)rd["MAPKM"],
        //                    LoaiPhieu = rd["LOAIPHIEU"].ToString(),
        //                    GiaTri = rd["GIATRI"] != DBNull.Value ? (int)rd["GIATRI"] : 0,
        //                    DieuKien = rd["DIEUKIEN"].ToString(),
        //                    NgayHetHan = (DateTime)rd["NGAYHETHAN"]
        //                });
        //            }
        //        }
        //    }

        //    return PartialView("_UuDai", list);
        //}
        //public ActionResult UuDaiSanPham(int id)
        //{
        //    List<UuDai> list = new List<UuDai>();

        //    using (var conn = new SqlConnection(_connKhach))
        //    using (var cmd = new SqlCommand("PROC_UUDAI_SANPHAM", conn))
        //    {
        //        cmd.CommandType = CommandType.StoredProcedure;
        //        cmd.Parameters.AddWithValue("@MaSP", id);

        //        conn.Open();
        //        using (var rd = cmd.ExecuteReader())
        //        {
        //            while (rd.Read())
        //            {
        //                list.Add(new UuDai
        //                {
        //                    MaPKM = (int)rd["MAPKM"],
        //                    LoaiPhieu = rd["LOAIPHIEU"].ToString(),
        //                    GiaTri = rd["GIATRI"] != DBNull.Value ? (int)rd["GIATRI"] : 0,
        //                    DieuKien = rd["DIEUKIEN"].ToString(),
        //                    NgayHetHan = (DateTime)rd["NGAYHETHAN"]
        //                });
        //            }
        //        }
        //    }

        //    return PartialView("_UuDai", list);
        //}
        //public ActionResult _UuDai()
        //{
        //    return View();
        //}
        //[HttpPost]
        //public ActionResult ChonKhuyenMai(int mapkm, int phantram)
        //{
        //    Session["MaKM"] = mapkm;
        //    Session["PhanTramKM"] = phantram;

        //    return Json(new { success = true });
        //}
    }
}

