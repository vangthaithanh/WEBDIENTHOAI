using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebDienThoai.Models;
using WebDienThoai.Models.ViewModels;

namespace WebDienThoai.DAL
{
    public class QuanLyKhoDAL
    {
        private readonly string _cs;

        public QuanLyKhoDAL(string connectionString)
        {
            _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        // ===== DTO phục vụ combobox + map JS =====
        public class SanPhamNhapDto
        {
            public int MASP { get; set; }
            public int MALOAI { get; set; }
            public string TENSP { get; set; }
            public decimal GIABAN { get; set; } // vẫn giữ để map (nếu bạn cần), nhưng View sẽ không hiển thị nữa
            public string ANH { get; set; }
        }

        public class LoaiSanPhamDto
        {
            public int MALOAI { get; set; }
            public string TENLOAI { get; set; }
        }

        // ===== KHO =====
        public List<Kho> GetKhoAll()
        {
            var list = new List<Kho>();
            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(@"SELECT MAKHO, TENKHO, DIACHI FROM dbo.KHO ORDER BY MAKHO", conn))
            {
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new Kho
                        {
                            MAKHO = rd["MAKHO"].ToString(),
                            TENKHO = rd["TENKHO"].ToString(),
                            DIACHI = rd["DIACHI"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        // ===== SANPHAM cho combobox =====
        public List<SanPhamNhapDto> SanPham_ListForNhap()
        {
            var list = new List<SanPhamNhapDto>();
            const string sql = @"
SELECT MASP, MALOAI, TENSP, GIABAN, ANH
FROM dbo.SANPHAM
ORDER BY MASP DESC;";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new SanPhamNhapDto
                        {
                            MASP = Convert.ToInt32(rd["MASP"]),
                            MALOAI = rd["MALOAI"] == DBNull.Value ? 0 : Convert.ToInt32(rd["MALOAI"]),
                            TENSP = rd["TENSP"]?.ToString(),
                            GIABAN = rd["GIABAN"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["GIABAN"]),
                            ANH = rd["ANH"] == DBNull.Value ? "" : rd["ANH"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        // ===== LOAI SP cho combobox (tự dò table) =====
        public List<LoaiSanPhamDto> LoaiSanPham_List()
        {
            var list = new List<LoaiSanPhamDto>();

            const string sql = @"
IF OBJECT_ID('dbo.LOAISP') IS NOT NULL
BEGIN
    SELECT MALOAI, TENLOAI FROM dbo.LOAISP ORDER BY MALOAI;
END
ELSE IF OBJECT_ID('dbo.LOAISANPHAM') IS NOT NULL
BEGIN
    SELECT MALOAI, TENLOAI FROM dbo.LOAISANPHAM ORDER BY MALOAI;
END
ELSE
BEGIN
    SELECT DISTINCT CAST(MALOAI AS INT) AS MALOAI,
           CAST(MALOAI AS NVARCHAR(100)) AS TENLOAI
    FROM dbo.SANPHAM
    WHERE MALOAI IS NOT NULL
    ORDER BY CAST(MALOAI AS INT);
END";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new LoaiSanPhamDto
                        {
                            MALOAI = Convert.ToInt32(rd["MALOAI"]),
                            TENLOAI = rd["TENLOAI"]?.ToString()
                        });
                    }
                }
            }

            return list;
        }

        // ===== Tạo mới sản phẩm (MASP tự sinh) =====
        // NOTE: GIABAN cho phép = 0 vì trigger sẽ tự cập nhật sau khi nhập hàng.
        public int SanPham_Create(int maLoai, string tenSp, decimal giaBan, string anhFile)
        {
            if (string.IsNullOrWhiteSpace(tenSp)) throw new ArgumentException("TENSP rỗng.");
            if (maLoai <= 0) throw new ArgumentException("MALOAI không hợp lệ.");
            if (giaBan < 0) throw new ArgumentException("GIABAN không hợp lệ."); // ✅ cho phép 0

            const string sql = @"
INSERT INTO dbo.SANPHAM (MALOAI, TENSP, GIABAN, ANH)
VALUES (@MALOAI, @TENSP, @GIABAN, @ANH);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@MALOAI", SqlDbType.Int).Value = maLoai;
                cmd.Parameters.Add("@TENSP", SqlDbType.NVarChar, 200).Value = tenSp.Trim();

                var pGia = cmd.Parameters.Add("@GIABAN", SqlDbType.Decimal);
                pGia.Precision = 18;
                pGia.Scale = 2;
                pGia.Value = giaBan; // có thể = 0

                cmd.Parameters.Add("@ANH", SqlDbType.NVarChar, 255).Value =
                    string.IsNullOrWhiteSpace(anhFile) ? (object)DBNull.Value : anhFile.Trim();

                conn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        // ===== TONKHO =====
        public List<TonKho> GetTonKho(string maKho = null)
        {
            var list = new List<TonKho>();
            var sql = @"SELECT MASP, MAKHO, SOLUONG
                        FROM dbo.TONKHO
                        WHERE (@MAKHO IS NULL OR MAKHO = @MAKHO)
                        ORDER BY MAKHO, MASP";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MAKHO", (object)maKho ?? DBNull.Value);
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new TonKho
                        {
                            MASP = rd["MASP"].ToString(),
                            MAKHO = rd["MAKHO"].ToString(),
                            SOLUONG = Convert.ToInt32(rd["SOLUONG"])
                        });
                    }
                }
            }
            return list;
        }

        // ===== PHIEUNHAP list =====
        public List<PhieuNhap> GetPhieuNhap(string maKho, DateTime? tuNgay, DateTime? denNgay)
        {
            var list = new List<PhieuNhap>();

            var sql = @"
SELECT pn.MAPHIEUNHAP, pn.ID, pn.NGAYNHAP, pn.NHACUNGCAP, pn.TONGGIA, pn.MAKHO,
       k.TENKHO
FROM dbo.PHIEUNHAP pn
LEFT JOIN dbo.KHO k ON k.MAKHO = pn.MAKHO
WHERE (@MAKHO IS NULL OR pn.MAKHO = @MAKHO)
  AND (@TUNGAY IS NULL OR pn.NGAYNHAP >= @TUNGAY)
  AND (@DENNGAY IS NULL OR pn.NGAYNHAP < DATEADD(DAY, 1, @DENNGAY))
ORDER BY pn.NGAYNHAP DESC, pn.MAPHIEUNHAP DESC";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MAKHO", (object)maKho ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TUNGAY", (object)tuNgay ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DENNGAY", (object)denNgay ?? DBNull.Value);

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new PhieuNhap
                        {
                            MAPHIEUNHAP = Convert.ToInt32(rd["MAPHIEUNHAP"]),
                            ID = rd["ID"] == DBNull.Value ? null : rd["ID"].ToString(),
                            NGAYNHAP = Convert.ToDateTime(rd["NGAYNHAP"]),
                            NHACUNGCAP = rd["NHACUNGCAP"].ToString(),
                            TONGGIA = Convert.ToDecimal(rd["TONGGIA"]),
                            MAKHO = rd["MAKHO"].ToString(),
                            TENKHO = rd["TENKHO"] == DBNull.Value ? null : rd["TENKHO"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        public PhieuNhap GetPhieuNhapById(int id)
        {
            var sql = @"
SELECT pn.MAPHIEUNHAP, pn.ID, pn.NGAYNHAP, pn.NHACUNGCAP, pn.TONGGIA, pn.MAKHO,
       k.TENKHO
FROM dbo.PHIEUNHAP pn
LEFT JOIN dbo.KHO k ON k.MAKHO = pn.MAKHO
WHERE pn.MAPHIEUNHAP = @ID";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new PhieuNhap
                    {
                        MAPHIEUNHAP = Convert.ToInt32(rd["MAPHIEUNHAP"]),
                        ID = rd["ID"] == DBNull.Value ? null : rd["ID"].ToString(),
                        NGAYNHAP = Convert.ToDateTime(rd["NGAYNHAP"]),
                        NHACUNGCAP = rd["NHACUNGCAP"].ToString(),
                        TONGGIA = Convert.ToDecimal(rd["TONGGIA"]),
                        MAKHO = rd["MAKHO"].ToString(),
                        TENKHO = rd["TENKHO"] == DBNull.Value ? null : rd["TENKHO"].ToString()
                    };
                }
            }
        }

        public List<ChiTietPN> GetChiTietPN(int maPhieuNhap)
        {
            var list = new List<ChiTietPN>();
            var sql = @"SELECT MAPHIEUNHAP, MASP, SOLUONG, GIANHAP
                        FROM dbo.CHITIETPN
                        WHERE MAPHIEUNHAP = @ID
                        ORDER BY MASP";

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ID", maPhieuNhap);
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new ChiTietPN
                        {
                            MAPHIEUNHAP = Convert.ToInt32(rd["MAPHIEUNHAP"]),
                            MASP = rd["MASP"].ToString(),
                            SOLUONG = Convert.ToInt32(rd["SOLUONG"]),
                            GIANHAP = Convert.ToDecimal(rd["GIANHAP"])
                        });
                    }
                }
            }
            return list;
        }

        // ===== Tạo phiếu nhập + cập nhật TONKHO (UPSERT) =====
        public int CreatePhieuNhap(int idNguoiTao, DateTime ngayNhap, string nhaCungCap, int maKho, List<ChiTietPN> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Items rỗng.");
            if (string.IsNullOrWhiteSpace(nhaCungCap)) throw new ArgumentException("Nhà cung cấp rỗng.");

            decimal tongGia = 0m;
            foreach (var it in items)
                tongGia += (decimal)it.SOLUONG * it.GIANHAP;

            using (var conn = new SqlConnection(_cs))
            {
                conn.Open();
                using (var cmdXact = new SqlCommand("SET XACT_ABORT ON;", conn))
                    cmdXact.ExecuteNonQuery();

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        const string insertPN = @"
INSERT INTO dbo.PHIEUNHAP (ID, NGAYNHAP, NHACUNGCAP, TONGGIA, MAKHO)
VALUES (@ID, @NGAYNHAP, @NCC, @TONGGIA, @MAKHO);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newId;
                        using (var cmd = new SqlCommand(insertPN, conn, tx))
                        {
                            cmd.Parameters.Add("@ID", SqlDbType.Int).Value = idNguoiTao;
                            cmd.Parameters.Add("@NGAYNHAP", SqlDbType.DateTime).Value = ngayNhap;
                            cmd.Parameters.Add("@NCC", SqlDbType.NVarChar, 100).Value = nhaCungCap.Trim();

                            var pTong = cmd.Parameters.Add("@TONGGIA", SqlDbType.Decimal);
                            pTong.Precision = 18;
                            pTong.Scale = 2;
                            pTong.Value = tongGia;

                            cmd.Parameters.Add("@MAKHO", SqlDbType.Int).Value = maKho;

                            newId = (int)cmd.ExecuteScalar();
                        }

                        const string insertCT = @"
INSERT INTO dbo.CHITIETPN (MAPHIEUNHAP, MASP, SOLUONG, GIANHAP)
VALUES (@MAPN, @MASP, @SL, @GIA);";

                        const string upsertTonKho = @"
IF EXISTS (SELECT 1 FROM dbo.TONKHO WITH (UPDLOCK, HOLDLOCK) WHERE MASP = @MASP AND MAKHO = @MAKHO)
    UPDATE dbo.TONKHO SET SOLUONG = SOLUONG + @SL WHERE MASP = @MASP AND MAKHO = @MAKHO;
ELSE
    INSERT INTO dbo.TONKHO (MASP, MAKHO, SOLUONG) VALUES (@MASP, @MAKHO, @SL);";

                        foreach (var it in items)
                        {
                            if (it == null) continue;

                            if (!int.TryParse(it.MASP?.ToString(), out int maspInt))
                                throw new FormatException($"MASP không hợp lệ: '{it?.MASP}' (phải là số).");

                            if (it.SOLUONG <= 0)
                                throw new ArgumentException($"Số lượng không hợp lệ cho MASP {maspInt}.");

                            if (it.GIANHAP <= 0)
                                throw new ArgumentException($"Giá nhập không hợp lệ cho MASP {maspInt}.");

                            using (var cmd = new SqlCommand(insertCT, conn, tx))
                            {
                                cmd.Parameters.Add("@MAPN", SqlDbType.Int).Value = newId;
                                cmd.Parameters.Add("@MASP", SqlDbType.Int).Value = maspInt;
                                cmd.Parameters.Add("@SL", SqlDbType.Int).Value = it.SOLUONG;

                                var pGia = cmd.Parameters.Add("@GIA", SqlDbType.Decimal);
                                pGia.Precision = 18;
                                pGia.Scale = 2;
                                pGia.Value = it.GIANHAP;

                                cmd.ExecuteNonQuery();
                            }

                            using (var cmdTk = new SqlCommand(upsertTonKho, conn, tx))
                            {
                                cmdTk.Parameters.Add("@MASP", SqlDbType.Int).Value = maspInt;
                                cmdTk.Parameters.Add("@MAKHO", SqlDbType.Int).Value = maKho;
                                cmdTk.Parameters.Add("@SL", SqlDbType.Int).Value = it.SOLUONG;
                                cmdTk.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                        return newId;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // ==== DonHangKho giữ nguyên nếu đang dùng ====
        public List<DonHangKhoItemVM> DonHangKho_List(int? maHD, DateTime? tuNgay, DateTime? denNgay, string trangThai)
        {
            var list = new List<DonHangKhoItemVM>();

            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand("dbo.usp_DonHangKho_List", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MAHD", (object)maHD ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TUNGAY", (object)tuNgay ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DENNGAY", (object)denNgay ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TRANGTHAI", string.IsNullOrWhiteSpace(trangThai) ? (object)DBNull.Value : trangThai);

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new DonHangKhoItemVM
                        {
                            MaHD = Convert.ToInt32(rd["MAHD"]),
                            NgayLap = Convert.ToDateTime(rd["NGAYLAP"]),
                            ThanhTien = Convert.ToDecimal(rd["THANHTIEN"]),
                            PhuongThucThanhToan = rd["PHUONGTHUCTHANHTOAN"]?.ToString(),
                            HoTen = rd["HOTEN"]?.ToString(),
                            SDT = rd["SDT"]?.ToString(),
                            DiaChi = rd["DIACHI"]?.ToString(),
                            TrangThaiGiaoHang = rd["TRANGTHAI"]?.ToString(),
                            NgayGiao = rd["NGAYGIAO"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["NGAYGIAO"])
                        });
                    }
                }
            }
            return list;
        }

        public void DonHangKho_UpdateTrangThai(int maHD, string trangThaiMoi)
        {
            using (var conn = new SqlConnection(_cs))
            using (var cmd = new SqlCommand("dbo.usp_DonHangKho_UpdateTrangThai", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@MAHD", SqlDbType.Int).Value = maHD;
                cmd.Parameters.Add("@TRANGTHAI_MOI", SqlDbType.NVarChar, 50).Value = trangThaiMoi;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ==== Thống kê doanh thu theo ngày ====
        public List<DoanhThuRowVM> ThongKeDoanhThu(DateTime? tuNgay, DateTime? denNgay, string trangThai = "Đã giao")
        {
            var rows = new Dictionary<DateTime, DoanhThuRowVM>();
            var orders = DonHangKho_List(null, tuNgay, denNgay, trangThai);
            foreach (var o in orders)
            {
                var day = o.NgayLap.Date;
                if (!rows.TryGetValue(day, out var r))
                {
                    r = new DoanhThuRowVM { Ngay = day, DoanhThu = 0m, SoDon = 0 };
                    rows[day] = r;
                }
                r.DoanhThu += o.ThanhTien;
                r.SoDon += 1;
            }
            var list = new List<DoanhThuRowVM>(rows.Values);
            list.Sort((a, b) => a.Ngay.CompareTo(b.Ngay));
            return list;
        }
    }
}
