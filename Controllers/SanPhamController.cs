using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebDienThoai.Models;

namespace WebDienThoai.Controllers
{
    public class SanPhamController : Controller
    {
        // GET: SanPham
        public ActionResult Index(string sort = "giathap", string series = null, int page = 1)
        {
            DbSanPham dbSp = new DbSanPham();

            bool hasMore;
            Dictionary<string, int> viewMap;
            Dictionary<string, int> discountMap;
            Dictionary<string, decimal> giaGocMap;

            var model = dbSp.DSSP(sort, series, page, 10, out hasMore,
                                  out viewMap, out discountMap, out giaGocMap);

            ViewBag.Sort = sort;
            ViewBag.Series = series;
            ViewBag.Page = page;
            ViewBag.HasMore = hasMore;

            ViewBag.ViewMap = viewMap;
            ViewBag.DiscountMap = discountMap;
            ViewBag.GiaGocMap = giaGocMap;

            return View(model);
        }

        public ActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return HttpNotFound();

            DbSanPham db = new DbSanPham();
            var sp = db.GetById(id);

            if (sp == null)
                return HttpNotFound();

            ViewBag.DanhGias = db.GetDanhGiasTheoSanPham(id);

            return View(sp); // Views/SanPham/Details.cshtml
        }
    }
}