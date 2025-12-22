using System;
using System.Collections.Generic;
using System.Linq;

namespace WebDienThoai.Models.ViewModels
{
    public class DonHangKhoFilterVM
    {
        public int? MaHD { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public string TrangThai { get; set; } // null = tất cả
    }

    public class DonHangKhoItemVM
    {
        public int MaHD { get; set; }
        public DateTime NgayLap { get; set; }
        public decimal ThanhTien { get; set; }
        public string PhuongThucThanhToan { get; set; }

        public string HoTen { get; set; }
        public string SDT { get; set; }
        public string DiaChi { get; set; }

        public string TrangThaiGiaoHang { get; set; }
        public DateTime? NgayGiao { get; set; }
    }

    public class DonHangKhoListVM
    {
        public DonHangKhoFilterVM Filter { get; set; } = new DonHangKhoFilterVM();
        public List<DonHangKhoItemVM> Items { get; set; } = new List<DonHangKhoItemVM>();

        public List<string> TrangThaiOptions { get; set; } = new List<string>
        {
            "Chờ xử lý", "Đang giao", "Đã giao", "Hủy"
        };
    }

    // ==== Thống kê doanh thu ====
    public class DoanhThuRowVM
    {
        public DateTime Ngay { get; set; }
        public decimal DoanhThu { get; set; }
        public int SoDon { get; set; }
    }

    public class ThongKeDoanhThuVM
    {
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public List<DoanhThuRowVM> Rows { get; set; } = new List<DoanhThuRowVM>();
        public decimal TongDoanhThu => Rows.Sum(r => r.DoanhThu);
        public int TongDon => Rows.Sum(r => r.SoDon);
    }
}
