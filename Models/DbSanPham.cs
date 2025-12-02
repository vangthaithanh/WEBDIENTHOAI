using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace WebDienThoai.Models
{
    public class DbSanPham
    {
        Test_webdtEntities t = new Test_webdtEntities();
        private int StableRandomInt(string key, int min, int max, string salt)
        {
            if (max < min) { int tmp = min; min = max; max = tmp; }
            if (min == max) return min;

            key = (key ?? "").Trim();
            salt = salt ?? "";

            using (var md5 = MD5.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(salt + "|" + key);
                byte[] hash = md5.ComputeHash(bytes);

                int val = BitConverter.ToInt32(hash, 0) & 0x7fffffff;
                int range = (max - min) + 1;
                return min + (val % range);
            }
        }

        private string ToSeriesKeyword(string series)
        {
            if (string.IsNullOrWhiteSpace(series)) return null;

            string s = series.Trim().ToLower();
            if (s.Contains("iphone16")) return "iphone 16";
            if (s.Contains("iphone15")) return "iphone 15";
            if (s.Contains("iphone14")) return "iphone 14";

            s = s.Replace("series", "").Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // KHÔNG tạo class VM, trả về List<SANPHAM> + out Dictionary để view dùng
        public List<SANPHAM> DSSP(
            string sort,
            string series,
            int page,
            int pageSize,
            out bool hasMore,
            out Dictionary<string, int> viewMap,
            out Dictionary<string, int> discountMap,
            out Dictionary<string, decimal> giaGocMap
        )
        {
            if (string.IsNullOrEmpty(sort)) sort = "giathap";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = t.SANPHAMs.AsQueryable();

            // Filter theo series (iphone14/15/16)
            string key = ToSeriesKeyword(series);
            if (!string.IsNullOrWhiteSpace(key))
            {
                string keyNoSpace = key.Replace(" ", "");
                query = query.Where(sp =>
                    (sp.TENSP ?? "").ToLower().Contains(key) ||
                    (sp.TENSP ?? "").ToLower().Contains(keyNoSpace)
                );
            }

            // Lấy hết danh sách đã filter -> tạo random map -> sort -> phân trang
            var all = query.ToList();

            viewMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            discountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            giaGocMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var sp in all)
            {
                string masp = (sp.MASP ?? "").Trim();

                int luotXem = StableRandomInt(masp, 200, 60000, "views");
                int giam = StableRandomInt(masp, 5, 60, "discount_percent");

                decimal giaBan = Convert.ToDecimal(sp.GIABAN);
                decimal giaGoc = giaBan + (giaBan * giam / 100m);

                viewMap[masp] = luotXem;
                discountMap[masp] = giam;
                giaGocMap[masp] = giaGoc;
            }
            var _viewMap = viewMap;
            var _discountMap = discountMap;

            IEnumerable<SANPHAM> ordered;
            switch (sort)
            {
                case "giacao":
                    ordered = all.OrderByDescending(sp => sp.GIABAN);
                    break;

                case "xemnhieu":
                    ordered = all.OrderByDescending(sp =>
                    {
                        int v;
                        return _viewMap.TryGetValue((sp.MASP ?? "").Trim(), out v) ? v : 0;
                    });
                    break;

                case "khuyenmai":
                    ordered = all.OrderByDescending(sp =>
                    {
                        int p;
                        return _discountMap.TryGetValue((sp.MASP ?? "").Trim(), out p) ? p : 0;
                    });
                    break;

                default: // giathap
                    ordered = all.OrderBy(sp => sp.GIABAN);
                    break;
            }

            int total = all.Count;
            hasMore = total > page * pageSize;

            return ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        public SANPHAM GetById(string masp)
        {
            // cho chắc chắn không bị trim / khoảng trắng
            masp = (masp ?? "").Trim();

            return t.SANPHAMs.FirstOrDefault(s => s.MASP.Trim() == masp);
        }
        public List<DANHGIA> GetDanhGiasTheoSanPham(string masp)
        {
            masp = (masp ?? "").Trim();

            var danhGias = (from d in t.DANHGIAs
                            join h in t.HOADONs on d.MAHD equals h.MAHD
                            join c in t.CHITIETHDs on new { h.MAHD, MASP = masp } equals new { c.MAHD, c.MASP }
                            join kh in t.KHACHHANGs on h.ID equals kh.ID
                            join nd in t.NGUOIDUNGs on kh.ID equals nd.ID
                            select new
                            {
                                d,
                                HOTEN = nd.HOTEN
                            }).ToList();

            foreach (var item in danhGias)
            {
                item.d.GHICHU = item.d.GHICHU ?? "";
                item.d.HOADON.KHACHHANG.NGUOIDUNG.HOTEN = item.HOTEN;
            }

            return danhGias.Select(x => x.d).ToList();
        }

    }
}