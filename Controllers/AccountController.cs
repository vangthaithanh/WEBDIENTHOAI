using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc;
using WebDienThoai.Models;

namespace WebDienThoai.Controllers
{
    public class AccountController : Controller
    {
        // Kết nối dùng riêng cho LOGIN
        private readonly string _connLogin =
            ConfigurationManager.ConnectionStrings["Conn_Admin"].ConnectionString;

        // Hàm trợ giúp để các chỗ khác dùng connection string theo role
        internal static string GetConnectionStringForCurrentUser(System.Web.SessionState.HttpSessionState session)
        {
            var name = session?["ConnName"] as string;
            if (string.IsNullOrEmpty(name))
                name = "Conn_Khach"; // mặc định là khách

            return ConfigurationManager.ConnectionStrings[name].ConnectionString;
        }

        // GET: /Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            ViewBag.Breadcrumb = "Đăng nhập";
            return View(new LoginViewModel());
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                using (var conn = new SqlConnection(_connLogin)) // LUÔN DÙNG CONN_ADMIN ĐỂ LOGIN
                using (var cmd = new SqlCommand("usp_Account_Login_WithRole", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TENTK", model.UserName);
                    cmd.Parameters.AddWithValue("@MATKHAU", model.Password);

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            string tentk = rd["TENTK"].ToString();
                            int idNguoiDung = rd["ID"] != DBNull.Value
                                              ? Convert.ToInt32(rd["ID"])
                                              : 0;
                            string hoTen = rd["HOTEN"] != DBNull.Value
                                           ? rd["HOTEN"].ToString()
                                           : tentk;

                            bool isNhanVien = rd["IS_NHANVIEN"] != DBNull.Value
                                              && Convert.ToInt32(rd["IS_NHANVIEN"]) == 1;
                            string roleCode = rd["ROLE_CODE"] != DBNull.Value
                                              ? rd["ROLE_CODE"].ToString()
                                              : "KHACH";

                            // Lưu thông tin chung
                            Session["UserName"] = tentk;
                            Session["UserId"] = idNguoiDung;
                            Session["HoTen"] = hoTen;
                            Session["IsNhanVien"] = isNhanVien;
                            Session["Role"] = roleCode;

                            // Chọn connection string theo role
                            string connName;
                            if (!isNhanVien || roleCode == "KHACH")
                            {
                                connName = "Conn_Khach";
                            }
                            else
                            {
                                switch (roleCode)
                                {
                                    case "ADMIN":
                                        connName = "Conn_Admin";
                                        break;
                                    case "NVKHO":
                                        connName = "Conn_Kho";
                                        break;
                                    case "NVSP":
                                        connName = "Conn_SanPham";
                                        break;
                                    default:
                                        connName = "Conn_Khach";
                                        break;
                                }
                            }

                            Session["ConnName"] = connName;

                            if (isNhanVien && roleCode != "KHACH")
                            {
                                return RedirectToAction("QuanTri", "Home");
                            }

                            // KHÁCH: về trang chủ (hoặc ReturnUrl nếu bạn đang dùng)
                            return RedirectToAction("Index", "Home");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                            return View(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View(model);
            }
        }

        // GET: /Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            ViewBag.Breadcrumb = "Đăng ký";
            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu nhập lại không khớp.");
                return View(model);
            }

            try
            {
                using (var conn = new SqlConnection(_connLogin))
                using (var cmd = new SqlCommand("usp_Account_Register", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@TENTK", model.UserName);
                    cmd.Parameters.AddWithValue("@MATKHAU", model.Password);

                    cmd.Parameters.AddWithValue("@HOTEN", (object)model.HoTen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SDT", (object)model.SDT ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EMAIL", (object)model.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DIACHI", (object)model.DiaChi ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GIOITINH", (object)model.GioiTinh ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DANTOC", (object)model.DanToc ?? DBNull.Value);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                TempData["RegisterSuccess"] = "Đăng ký thành công, vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            catch (SqlException ex)
            {
                var msg = ex.Message;

                if (msg.Contains("Tên đăng nhập đã tồn tại"))
                {
                    ModelState.AddModelError("UserName", msg);
                }
                else if (msg.Contains("Số điện thoại này đã được sử dụng"))
                {
                    ModelState.AddModelError("SDT", msg);
                }
                else if (msg.Contains("Email này đã được sử dụng"))
                {
                    ModelState.AddModelError("Email", msg);
                }
                else if (msg.Contains("UQ_NGUOIDUNG_SDT"))
                {
                    // lỗi từ UNIQUE constraint SDT
                    ModelState.AddModelError("SDT", "Số điện thoại này đã được sử dụng, vui lòng nhập số khác.");
                }
                else if (msg.Contains("UQ_NGUOIDUNG_EMAIL")) // nếu bạn có unique cho email
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng, vui lòng nhập email khác.");
                }
                else
                {
                    ModelState.AddModelError("", "Dữ liệu không hợp lệ: " + msg);
                }

                return View(model);
            }


            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View(model);
            }
        }

        // GET: /Account/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }
        public ActionResult Backup()
        {
            if (Session["Role"] == null || Session["Role"].ToString() != "ADMIN")
                return new HttpUnauthorizedResult("Bạn không có quyền truy cập trang này!");
            return View();
        }

        // lấy thông tin cá nhân (dùng Conn_Admin để tránh lỗi quyền theo role)
        private ThongTinNguoiDung GetUserProfile(string tentk)
        {
            if (string.IsNullOrWhiteSpace(tentk)) return null;
            using (var conn = new SqlConnection(_connLogin))
            using (var cmd = new SqlCommand("usp_User_GetProfile", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 50).Value = tentk.Trim();

                conn.Open();
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!rd.Read()) return null;
                    var vm = new ThongTinNguoiDung
                    {
                        Id = rd["ID"] == DBNull.Value ? 0 : Convert.ToInt32(rd["ID"]),
                        TenTK = rd["TENTK"] == DBNull.Value ? null : rd["TENTK"].ToString(),
                        HoTen = rd["HOTEN"] == DBNull.Value ? null : rd["HOTEN"].ToString(),
                        SDT = rd["SDT"] == DBNull.Value ? null : rd["SDT"].ToString(),
                        Email = rd["EMAIL"] == DBNull.Value ? null : rd["EMAIL"].ToString(),
                        DiaChi = rd["DIACHI"] == DBNull.Value ? null : rd["DIACHI"].ToString(),
                        GioiTinh = rd["GIOITINH"] == DBNull.Value ? null : rd["GIOITINH"].ToString(),
                        DanToc = rd["DANTOC"] == DBNull.Value ? null : rd["DANTOC"].ToString(),
                        IsNhanVien = rd["IS_NHANVIEN"] != DBNull.Value && Convert.ToInt32(rd["IS_NHANVIEN"]) == 1,
                        ChucVu = rd["CHUCVU"] == DBNull.Value ? null : rd["CHUCVU"].ToString(),
                        NgayVaoLam = rd["NGAYVAOLAM"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["NGAYVAOLAM"]),
                        TongDonDaMua = rd["TONG_DON"] == DBNull.Value ? 0 : Convert.ToInt32(rd["TONG_DON"]),
                        Diem = rd["DIEM"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["DIEM"]),
                        RoleCode = rd["ROLE_CODE"] == DBNull.Value ? "KHACH" : rd["ROLE_CODE"].ToString()
                    };
                    return vm;
                }
            }
        }
        [HttpGet]
        public ActionResult ThongTinCaNhan()
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
                return RedirectToAction("Login", "Account");
            var vm = GetUserProfile(tentk);
            if (vm == null)
                return HttpNotFound("Không tìm thấy thông tin người dùng.");
            ViewBag.BreadcrumbList = new List<BreadcrumbItem>
    {
        new BreadcrumbItem { Text = "Trang chủ", Action = "Index", Controller = "Home" },
        new BreadcrumbItem { Text = "Tài khoản" },
        new BreadcrumbItem { Text = "Thông tin cá nhân" }
    };
            return View(vm);
        }
        [HttpGet]
        public ActionResult CapNhatThongTinCaNhan()
        {
            if (Session["UserName"] == null)
            {
                TempData["Error"] = "Vui lòng đăng nhập để cập nhật thông tin.";
                return RedirectToAction("Login");
            }
            string userName = Session["UserName"].ToString();
            var model = GetUserProfile(userName); // Lấy dữ liệu profile hiện tại
            if (model == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin hồ sơ để cập nhật.";
                return RedirectToAction("ThongTinCaNhan");
            }
            ViewBag.Breadcrumb = "Cập nhật thông tin cá nhân";
            return View("CapNhatThongTinCaNhan", model); // Trả về View CNThongTinCaNhan.cshtml
        }

        //cập nhật thông tin cá nhân
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CapNhatThongTinCaNhan(ThongTinNguoiDung input)
        {
            if (Session["UserName"] == null)
            {
                return RedirectToAction("Login");
            }
            input.TenTK = Session["UserName"].ToString();

            // Dùng ValidationSummary, nếu có lỗi model thì trả về 
            if (!ModelState.IsValid)
            {
                ViewBag.Breadcrumb = "Cập nhật thông tin cá nhân";
                //  Trả về View với dữ liệu đã nhập 
                return View("CapNhatThongTinCaNhan", input);
            }
            try
            {
                using (var conn = new SqlConnection(_connLogin))
                using (var cmd = new SqlCommand("usp_User_UpdateProfile", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 50).Value = input.TenTK;
                    cmd.Parameters.Add("@HOTEN", SqlDbType.NVarChar, 100).Value = (object)input.HoTen ?? DBNull.Value;
                    cmd.Parameters.Add("@SDT", SqlDbType.VarChar, 15).Value = (object)input.SDT ?? DBNull.Value; // ĐÃ SỬA: từ input.Sdt thành input.SDT
                    cmd.Parameters.Add("@EMAIL", SqlDbType.VarChar, 100).Value = (object)input.Email ?? DBNull.Value;
                    cmd.Parameters.Add("@DIACHI", SqlDbType.NVarChar, 255).Value = (object)input.DiaChi ?? DBNull.Value;
                    cmd.Parameters.Add("@GIOITINH", SqlDbType.NVarChar, 10).Value = (object)input.GioiTinh ?? DBNull.Value;
                    cmd.Parameters.Add("@DANTOC", SqlDbType.NVarChar, 30).Value = (object)input.DanToc ?? DBNull.Value;
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        int code = Convert.ToInt32(rd["Code"]);
                        string msg = rd["Msg"].ToString();
                        if (code != 0)
                        {
                            TempData["Error"] = msg;
                            ViewBag.Breadcrumb = "Cập nhật thông tin cá nhân";
                            // Trả về View với dữ liệu đã nhập
                            return View("CapNhatThongTinCaNhan", input);
                        }
                        //  Cập nhật lại Session và thông báo
                        if (!string.IsNullOrWhiteSpace(input.HoTen))
                            Session["HoTen"] = input.HoTen;
                        TempData["Success"] = "Cập nhật thông tin cá nhân thành công!";
                        return RedirectToAction("ThongTinCaNhan"); // Chuyển hướng về trang hiển thị
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi hệ thống: " + ex.Message;
                ViewBag.Breadcrumb = "Cập nhật thông tin cá nhân";
                return View("CapNhatThongTinCaNhan", input);
            }
        }
        //đổi mật khẩu
        [HttpGet]
        public ActionResult DoiMatKhau()
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
                return RedirectToAction("Login", "Account");
            ViewBag.BreadcrumbList = new List<WebDienThoai.Models.BreadcrumbItem>
    {
        new WebDienThoai.Models.BreadcrumbItem { Text = "Trang chủ", Action = "Index", Controller = "Home" },
        new WebDienThoai.Models.BreadcrumbItem { Text = "Tài khoản" },
        new WebDienThoai.Models.BreadcrumbItem { Text = "Đổi mật khẩu" }
    };
            return View(new WebDienThoai.Models.DoiMatKhau());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoiMatKhau(WebDienThoai.Models.DoiMatKhau model)
        {
            var tentk = Session["UserName"] as string;
            if (string.IsNullOrWhiteSpace(tentk))
                return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid)
                return View(model);
            try
            {
                using (var conn = new SqlConnection(_connLogin))
                using (var cmd = new SqlCommand("usp_Account_ChangePassword", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@TENTK", SqlDbType.VarChar, 100).Value = tentk;
                    cmd.Parameters.Add("@OLDPASS", SqlDbType.NVarChar, 200).Value = model.OldPassword ?? "";
                    cmd.Parameters.Add("@NEWPASS", SqlDbType.NVarChar, 200).Value = model.NewPassword ?? "";
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            TempData["Error"] = "Đổi mật khẩu thất bại";
                            return View(model);
                        }
                        int code = Convert.ToInt32(rd["Code"]);
                        string msg = rd["Msg"]?.ToString();
                        if (code != 0)
                        {
                            TempData["Error"] = msg;
                            return View(model);
                        }
                        TempData["Success"] = msg ?? "Đổi mật khẩu thành công!";
                        return RedirectToAction("ThongTinCaNhan"); // hoặc quay lại trang tài khoản của bạn
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return View(model);
            }
        }
        [ChildActionOnly]
        public PartialViewResult ThongTinCNPartial()
        {
            if (Session["UserName"] == null)
                return PartialView("_ThongTinCN", new ThongTinNguoiDung());
            string tentk = Session["UserName"].ToString();
            var vm = GetUserProfile(tentk) ?? new ThongTinNguoiDung();
            return PartialView("_ThongTinCN", vm);
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            ViewBag.Breadcrumb = "Quên mật khẩu";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                // Generate OTP first so it is available after DB operations
                var otp = new Random().Next(100000, 999999).ToString();

                using (var conn = new SqlConnection(_connLogin))
                {
                    conn.Open();

                    // 1) verify username + email
                    using (var checkCmd = new SqlCommand("dbo.usp_Account_FindUserByUserNameEmail", conn))
                    {
                        checkCmd.CommandType = CommandType.StoredProcedure;
                        checkCmd.Parameters.AddWithValue("@TENTK", vm.UserName);
                        checkCmd.Parameters.AddWithValue("@EMAIL", vm.Email);
                        var exists = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0) == 1;
                        if (!exists)
                        {
                            ModelState.AddModelError("", "Không tìm thấy tài khoản khớp với email.");
                            return View(vm);
                        }
                    }

                    // 2) persist OTP via proc
                    using (var otpCmd = new SqlCommand("dbo.usp_Account_SetResetOtp", conn))
                    {
                        otpCmd.CommandType = CommandType.StoredProcedure;
                        otpCmd.Parameters.AddWithValue("@TENTK", vm.UserName);
                        otpCmd.Parameters.AddWithValue("@OTP", otp);
                        otpCmd.Parameters.AddWithValue("@EXPIRES_MINUTES", 10);
                        otpCmd.ExecuteNonQuery();
                    }
                }

                EmailService.SendOtpEmail(vm.Email, vm.UserName, otp, 10);
                TempData["Info"] = "OTP đã được gửi tới email.";
                return RedirectToAction("ResetPasswordOtp", new { userName = vm.UserName });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public ActionResult ResetPasswordOtp(string userName)
        {
            var model = new ResetPasswordWithOtpViewModel { UserName = userName };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPasswordOtp(ResetPasswordWithOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using (var conn = new SqlConnection(_connLogin))
                using (var cmd = new SqlCommand("dbo.usp_Account_ResetPassword_WithOtp", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TENTK", model.UserName);
                    cmd.Parameters.AddWithValue("@OTP", model.Otp);
                    cmd.Parameters.AddWithValue("@NEWPASS", model.NewPassword);
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            ModelState.AddModelError("", "Đặt lại mật khẩu thất bại.");
                            return View(model);
                        }
                        int code = Convert.ToInt32(rd["Code"]);
                        string msg = rd["Msg"].ToString();
                        if (code != 0)
                        {
                            ModelState.AddModelError("", msg);
                            return View(model);
                        }
                        TempData["Success"] = msg;
                        return RedirectToAction("Login");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                return View(model);
            }
        }
    }

}
