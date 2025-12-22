using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using WebDienThoai.Models;

namespace WebDienThoai.Controllers
{
    public class QuanLyNhanSuController : Controller
    {
        private string GetConnStr()
        {
            // lấy tên connection lưu trong Session khi login
            var connName = Session["ConnName"] as string;

            if (string.IsNullOrEmpty(connName))
                throw new InvalidOperationException("Không tìm thấy ConnName trong session.");

            var cs = ConfigurationManager.ConnectionStrings[connName];
            if (cs == null)
                throw new InvalidOperationException("Không tìm thấy chuỗi kết nối: " + connName);

            return cs.ConnectionString;
        }

        private bool IsAdmin()
        {
            var role = Session["Role"] as string;
            return !string.IsNullOrEmpty(role) &&
                   role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase);
        }

        // GET: QuanLyNhanSu
        public ActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            ViewBag.BreadcrumbList = new List<WebDienThoai.Models.BreadcrumbItem>
        {
            new WebDienThoai.Models.BreadcrumbItem { Text = "Trang chủ", Action = "Index", Controller = "Home" },
            new WebDienThoai.Models.BreadcrumbItem { Text = "Quản lý nhân sự" }
        };

            var list = new List<NhanVienListItemViewModel>();

            using (var conn = new SqlConnection(GetConnStr()))
            using (var cmd = new SqlCommand("dbo.usp_User_ListWithRole", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new NhanVienListItemViewModel
                        {
                            ID = Convert.ToInt32(rd["ID"]),
                            UserName = rd["UserName"]?.ToString(),
                            HoTen = rd["HoTen"]?.ToString(),
                            SDT = rd["SDT"]?.ToString(),
                            Email = rd["Email"]?.ToString(),
                            ChucVu = rd["ChucVu"]?.ToString(),
                            NgayVaoLam = rd["NgayVaoLam"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["NgayVaoLam"]),
                            RoleCode = rd["RoleCode"]?.ToString()
                        });
                    }
                }
            }

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeRole(int id, string roleCode)
        {
            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            try
            {
                using (var conn = new SqlConnection(GetConnStr()))
                using (var cmd = new SqlCommand("dbo.usp_User_ChangeRole", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@ROLE_CODE", SqlDbType.VarChar, 10).Value = roleCode;

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                TempData["Success"] = $"Đã đổi role cho ID={id} -> {roleCode}";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = "Lỗi đổi role: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // GET: QuanLyNhanSu/Create
        public ActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            // breadcrumb
            ViewBag.BreadcrumbList = new List<WebDienThoai.Models.BreadcrumbItem>
            {
                new WebDienThoai.Models.BreadcrumbItem { Text = "Trang chủ", Action = "Index", Controller = "Home" },
                new WebDienThoai.Models.BreadcrumbItem { Text = "Quản lý nhân sự", Action = "Index", Controller = "QuanLyNhanSu" },
                new WebDienThoai.Models.BreadcrumbItem { Text = "Tạo nhân viên" }
            };

            FillRoleDropDown();
            var model = new RegisterNhanVienViewModel();
            return View(model);
        }

        // POST: QuanLyNhanSu/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(RegisterNhanVienViewModel model)
        {
            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
            {
                FillRoleDropDown();
                return View(model);
            }

            try
            {
                using (var conn = new SqlConnection(GetConnStr()))
                using (var cmd = new SqlCommand("dbo.usp_NhanVien_Register", conn))
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

                    cmd.Parameters.AddWithValue("@CHUCVU", model.ChucVu);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                TempData["Success"] = "Tạo tài khoản nhân viên thành công.";
                return RedirectToAction("Index");
            }
            catch (SqlException ex)
            {
                // Bắt message từ RAISERROR trong proc (ví dụ: 'Tên tài khoản đã tồn tại.')
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
            }

            FillRoleDropDown();
            return View(model);
        }

        private void FillRoleDropDown()
        {
            ViewBag.RoleList = new List<SelectListItem>
            {
                new SelectListItem { Text = "Nhân viên kho",      Value = "Nhân viên Kho" },
                new SelectListItem { Text = "Nhân viên bán hàng", Value = "Nhân viên bán hàng" }
            };
        }
    }
}