using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using WebDienThoai.DAL;
using WebDienThoai.Models;
using WebDienThoai.Models.ViewModels;

namespace WebDienThoai.Controllers
{
    public class QuanLyKhoController : Controller
    {
        private const string NEW_VALUE = "__NEW__";
        private QuanLyKhoDAL _dal;

        private string GetConnStr()
        {
            var connName = Session["ConnName"] as string;
            if (string.IsNullOrWhiteSpace(connName))
                connName = "Conn_Admin";

            var cs = ConfigurationManager.ConnectionStrings[connName]
                  ?? ConfigurationManager.ConnectionStrings["Conn_Admin"];

            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new InvalidOperationException("Thiếu connection string 'Conn_Admin' (hoặc ConnName) trong Web.config.");

            return cs.ConnectionString;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            _dal = new QuanLyKhoDAL(GetConnStr());
            base.OnActionExecuting(filterContext);
        }

        private void LoadPhieuNhapLookup()
        {
            var sp = _dal.SanPham_ListForNhap();
            ViewBag.SanPhamJson = sp;

            ViewBag.SanPhamSelect = sp.Select(x => new SelectListItem
            {
                Value = x.MASP.ToString(),
                Text = $"{x.MASP} - {x.TENSP}"
            }).ToList();

            var loai = _dal.LoaiSanPham_List();
            ViewBag.LoaiSelect = loai.Select(x => new SelectListItem
            {
                Value = x.MALOAI.ToString(),
                Text = $"{x.MALOAI} - {x.TENLOAI}"
            }).ToList();
        }

        // ===== Upload ảnh SP mới: name="NewAnhFiles" + hidden name="NewAnhRowIdx" =====
        // Lưu ý: View sẽ DISABLE hidden/file nếu dòng không phải __NEW__ -> tránh lệch index.
        private Dictionary<int, HttpPostedFileBase> GetNewAnhFilesByRowIndex()
        {
            var map = new Dictionary<int, HttpPostedFileBase>();

            var idxVals = Request.Form.GetValues("NewAnhRowIdx") ?? new string[0];

            // lấy các file có key = NewAnhFiles theo đúng thứ tự submit
            var files = new List<HttpPostedFileBase>();
            for (int i = 0; i < Request.Files.Count; i++)
            {
                var key = Request.Files.AllKeys[i] ?? "";
                if (!string.Equals(key, "NewAnhFiles", StringComparison.OrdinalIgnoreCase))
                    continue;

                files.Add(Request.Files[i]);
            }

            var n = Math.Min(idxVals.Length, files.Count);
            for (int k = 0; k < n; k++)
            {
                if (!int.TryParse(idxVals[k], out int rowIdx)) continue;

                var f = files[k];
                if (f != null && f.ContentLength > 0)
                    map[rowIdx] = f;
            }

            return map;
        }

        // fallback kiểu cũ nếu bạn còn chỗ dùng name="NewProducts[i].ANHFILE"
        private Dictionary<int, HttpPostedFileBase> GetNewProductFilesByIndex()
        {
            var dict = new Dictionary<int, HttpPostedFileBase>();
            var keys = Request.Files.AllKeys;

            for (int k = 0; k < keys.Length; k++)
            {
                var key = keys[k] ?? "";
                if (string.IsNullOrWhiteSpace(key)) continue;

                var m = Regex.Match(key, @"NewProducts\[(\d+)\]\.ANHFILE", RegexOptions.IgnoreCase);
                if (!m.Success) continue;

                if (!int.TryParse(m.Groups[1].Value, out int idx)) continue;

                var f = Request.Files[k];
                if (f != null && f.ContentLength > 0)
                    dict[idx] = f;
            }

            return dict;
        }

        private Dictionary<int, HttpPostedFileBase> GetNewFilesSmart()
        {
            var a = GetNewAnhFilesByRowIndex();
            if (a.Count > 0) return a;
            return GetNewProductFilesByIndex();
        }

        private string SaveUploadedSanPhamImage(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0)
                throw new InvalidOperationException("File ảnh không hợp lệ.");

            const int maxBytes = 10 * 1024 * 1024; // 10MB
            if (file.ContentLength > maxBytes)
                throw new InvalidOperationException("Ảnh quá lớn (tối đa 10MB).");

            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",".jpeg",".png",".webp",".gif",".jfif",".bmp"
            };
            if (!allow.Contains(ext))
                throw new InvalidOperationException("Định dạng ảnh không hỗ trợ. Chỉ: jpg, jpeg, png, webp, gif, jfif, bmp.");

            var dir = Server.MapPath("~/Content/Anh/sanpham");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var safeName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, safeName);

            file.SaveAs(fullPath);
            return safeName;
        }

        // ===== Index / các màn khác (giữ như bạn đang dùng) =====
        public ActionResult Index()
        {
            var kho = _dal.GetKhoAll();
            return View(kho);
        }

        public ActionResult TonKho(string maKho)
        {
            var khoList = _dal.GetKhoAll();
            var vm = new TonKhoVM
            {
                MAKHO = maKho,
                KhoList = khoList,
                Items = _dal.GetTonKho(maKho)
            };

            if (!string.IsNullOrWhiteSpace(maKho))
                vm.TENKHO = khoList.FirstOrDefault(x => x.MAKHO == maKho)?.TENKHO;

            return View(vm);
        }

        public ActionResult PhieuNhap(string maKho, DateTime? tuNgay, DateTime? denNgay)
        {
            var vm = new PhieuNhapListVM
            {
                MAKHO = maKho,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                KhoList = _dal.GetKhoAll(),
                Items = _dal.GetPhieuNhap(maKho, tuNgay, denNgay)
            };

            return View(vm);
        }

        public ActionResult ChiTietPhieuNhap(int id)
        {
            var pn = _dal.GetPhieuNhapById(id);
            if (pn == null) return HttpNotFound();

            var vm = new PhieuNhapDetailVM
            {
                PhieuNhap = pn,
                ChiTiet = _dal.GetChiTietPN(id)
            };
            return View(vm);
        }

        [HttpGet]
        public ActionResult InPhieuNhap(int id)
        {
            var pn = _dal.GetPhieuNhapById(id);
            if (pn == null) return HttpNotFound();

            var vm = new PhieuNhapDetailVM
            {
                PhieuNhap = pn,
                ChiTiet = _dal.GetChiTietPN(id)
            };

            return View("InPhieuNhap", vm);
        }

        // =========================
        // GET: TaoPhieuNhap
        // =========================
        [HttpGet]
        public ActionResult TaoPhieuNhap()
        {
            var vm = new CreatePhieuNhapVM
            {
                NGAYNHAP = DateTime.Now,
                KhoList = _dal.GetKhoAll()
            };

            LoadPhieuNhapLookup();
            return View(vm);
        }

        // =========================
        // POST: TaoPhieuNhap
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TaoPhieuNhap(CreatePhieuNhapVM vm)
        {
            vm.KhoList = _dal.GetKhoAll();
            LoadPhieuNhapLookup();

            vm.Items = (vm.Items ?? new List<CreatePhieuNhapItemVM>())
                        .Where(x => x != null)
                        .ToList();

            // giữ lại dữ liệu SP mới để render lại (trừ file)
            var newVals = new Dictionary<string, string>();

            // lấy file theo map ổn định
            var filesByIdx = GetNewFilesSmart();

            // ===== validate header =====
            if (string.IsNullOrWhiteSpace(vm.MAKHO))
                ModelState.AddModelError("MAKHO", "Vui lòng chọn kho.");

            if (string.IsNullOrWhiteSpace(vm.NHACUNGCAP))
                ModelState.AddModelError("NHACUNGCAP", "Vui lòng nhập nhà cung cấp.");

            if (Session["UserId"] == null)
                ModelState.AddModelError("", "Bạn cần đăng nhập (có ID người tạo) để tạo phiếu nhập.");

            if (vm.Items.Count == 0)
                ModelState.AddModelError("", "Cần ít nhất 1 dòng sản phẩm.");

            // ===== validate items =====
            for (int i = 0; i < vm.Items.Count; i++)
            {
                var it = vm.Items[i];
                var maspRaw = (it.MASP ?? "").Trim();

                if (string.IsNullOrWhiteSpace(maspRaw))
                {
                    ModelState.AddModelError($"Items[{i}].MASP", "Vui lòng chọn MASP.");
                }
                else if (!string.Equals(maspRaw, NEW_VALUE, StringComparison.Ordinal))
                {
                    if (!int.TryParse(maspRaw, out _))
                        ModelState.AddModelError($"Items[{i}].MASP", "MASP không hợp lệ (phải là số).");
                }
                else
                {
                    // SP mới: chỉ cần TENSP + MALOAI + ẢNH (GIABAN bỏ)
                    var ten = (Request.Form[$"NewProducts[{i}].TENSP"] ?? "").Trim();
                    var maloaiStr = (Request.Form[$"NewProducts[{i}].MALOAI"] ?? "").Trim();

                    newVals[$"{i}.TENSP"] = ten;
                    newVals[$"{i}.MALOAI"] = maloaiStr;

                    if (string.IsNullOrWhiteSpace(ten))
                        ModelState.AddModelError($"Items[{i}].MASP", "SP mới: chưa nhập Tên SP.");

                    if (!int.TryParse(maloaiStr, out int maloaiInt) || maloaiInt <= 0)
                        ModelState.AddModelError($"Items[{i}].MASP", "SP mới: Mã loại không hợp lệ.");

                    if (!filesByIdx.TryGetValue(i, out var f) || f == null || f.ContentLength <= 0)
                        ModelState.AddModelError($"Items[{i}].MASP", "SP mới: Vui lòng chọn ảnh (Browse từ máy).");
                }

                if (it.SOLUONG <= 0)
                    ModelState.AddModelError($"Items[{i}].SOLUONG", "Số lượng phải >= 1.");

                if (it.GIANHAP <= 0)
                    ModelState.AddModelError($"Items[{i}].GIANHAP", "Giá nhập phải > 0.");
            }

            ViewBag.NewProducts = newVals;

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);

                // MAKHO đang là string nhưng DB của bạn đang dùng kiểu số -> Convert.ToInt32 như code cũ của bạn
                int maKhoInt = Convert.ToInt32(vm.MAKHO);

                DateTime ngayNhap = vm.NGAYNHAP ?? DateTime.Now;

                var items = new List<ChiTietPN>();

                for (int i = 0; i < vm.Items.Count; i++)
                {
                    var maspRaw = (vm.Items[i].MASP ?? "").Trim();
                    int maspInt;

                    if (string.Equals(maspRaw, NEW_VALUE, StringComparison.Ordinal))
                    {
                        var ten = newVals[$"{i}.TENSP"].Trim();
                        int maloaiInt = Convert.ToInt32(newVals[$"{i}.MALOAI"]);

                        if (!filesByIdx.TryGetValue(i, out var file) || file == null || file.ContentLength <= 0)
                            throw new InvalidOperationException("Thiếu file ảnh upload (SP mới).");

                        var savedFileName = SaveUploadedSanPhamImage(file);

                        // GIABAN = 0 (trigger sẽ tự cập nhật theo phiếu nhập)
                        maspInt = _dal.SanPham_Create(maloaiInt, ten, 0m, savedFileName);
                    }
                    else
                    {
                        maspInt = Convert.ToInt32(maspRaw);
                    }

                    items.Add(new ChiTietPN
                    {
                        MASP = maspInt.ToString(),
                        SOLUONG = vm.Items[i].SOLUONG,
                        GIANHAP = vm.Items[i].GIANHAP
                    });
                }

                int newId = _dal.CreatePhieuNhap(
                    userId,
                    ngayNhap,
                    vm.NHACUNGCAP.Trim(),
                    maKhoInt,
                    items
                );

                TempData["Success"] = "Tạo phiếu nhập thành công.";
                return RedirectToAction("ChiTietPhieuNhap", new { id = newId });
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Có lỗi khi tạo phiếu nhập (SQL): " + ex.Message);
                return View(vm);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi khi tạo phiếu nhập: " + ex.Message);
                return View(vm);
            }
        }

        // ===== DonHang giữ nguyên nếu đang dùng =====
        public ActionResult DonHang(int? maHD, DateTime? tuNgay, DateTime? denNgay, string trangThai)
        {
            var vm = new DonHangKhoListVM();
            vm.Filter.MaHD = maHD;
            vm.Filter.TuNgay = tuNgay;
            vm.Filter.DenNgay = denNgay;
            vm.Filter.TrangThai = trangThai;

            vm.Items = _dal.DonHangKho_List(maHD, tuNgay, denNgay, trangThai);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CapNhatTrangThaiDonHang(int maHD, string trangThaiMoi, int? maHDFilter, DateTime? tuNgay, DateTime? denNgay, string trangThai)
        {
            try
            {
                _dal.DonHangKho_UpdateTrangThai(maHD, trangThaiMoi);
                TempData["Success"] = $"Đã cập nhật MAHD {maHD} -> {trangThaiMoi}";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = "Lỗi cập nhật: " + ex.Message;
            }

            return RedirectToAction("DonHang", new { maHD = maHDFilter, tuNgay, denNgay, trangThai });
        }

        // ==== Thống kê doanh thu ====
        [HttpGet]
        public ActionResult ThongKeDoanhThu(DateTime? tuNgay, DateTime? denNgay)
        {
            var vm = new ThongKeDoanhThuVM
            {
                TuNgay = tuNgay,
                DenNgay = denNgay,
                Rows = _dal.ThongKeDoanhThu(tuNgay, denNgay)
            };
            return View(vm);
        }
    }
}
