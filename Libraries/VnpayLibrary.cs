using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using WebDienThoai.Models.Vnpay;

namespace WebDienThoai.Libraries
{
    public class VnpayLibrary
    {
        private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

        // MVC5: QueryString là NameValueCollection
        // rawQuery: HttpContext.Current.Request.Url.Query (giữ nguyên encode từ VNPAY)
        public PaymentResponseModel GetFullResponseData(NameValueCollection collection, string hashSecret, string rawQuery = null)
        {
            var vnPay = new VnpayLibrary();

            foreach (string key in collection.AllKeys)
            {
                var value = collection[key];
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnPay.AddResponseData(key, value);
                }
            }

            var vnpSecureHash = collection["vnp_SecureHash"]; // hash của dữ liệu trả về
            var checkSignature = vnPay.ValidateSignature(vnpSecureHash, hashSecret, rawQuery); // check Signature

            var vnpResponseCode = vnPay.GetResponseData("vnp_ResponseCode");

            if (!checkSignature)
                return new PaymentResponseModel()
                {
                    Success = false,
                    Token = vnpSecureHash,
                    VnPayResponseCode = vnpResponseCode
                };

            var orderInfo = vnPay.GetResponseData("vnp_OrderInfo");

            // NOTE: có thể TxnRef/TransactionNo không parse được nếu rỗng => dùng string trước
            var orderIdStr = vnPay.GetResponseData("vnp_TxnRef");
            var vnPayTranIdStr = vnPay.GetResponseData("vnp_TransactionNo");

            return new PaymentResponseModel()
            {
                Success = true,
                PaymentMethod = "VnPay",
                OrderDescription = orderInfo,
                OrderId = orderIdStr,
                PaymentId = vnPayTranIdStr,
                TransactionId = vnPayTranIdStr,
                Token = vnpSecureHash,
                VnPayResponseCode = vnpResponseCode
            };
        }

        // MVC5: lấy IP theo System.Web
        public string GetIpAddress(HttpContext context)
        {
            try
            {
                var forwarded = context?.Request?.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (!string.IsNullOrWhiteSpace(forwarded))
                    return forwarded.Split(',')[0].Trim();

                var ip = context?.Request?.UserHostAddress;
                return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (_requestData.ContainsKey(key)) _requestData[key] = value;
                else _requestData.Add(key, value);
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (_responseData.ContainsKey(key)) _responseData[key] = value;
                else _responseData.Add(key, value);
            }
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
        }

        public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
        {
            var data = new StringBuilder();

            foreach (var kv in _requestData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            var querystring = data.ToString();
            baseUrl += "?" + querystring;

            var signData = querystring;
            if (signData.Length > 0)
            {
                signData = signData.Remove(data.Length - 1, 1); // bỏ '&' cuối
            }

            var vnpSecureHash = HmacSha512(vnpHashSecret, signData);
            baseUrl += "vnp_SecureHash=" + vnpSecureHash;

            return baseUrl;
        }

        // rawQuery dùng để tránh sai khác encode/decode trong MVC5
        public bool ValidateSignature(string inputHash, string secretKey, string rawQuery = null)
        {
            if (string.IsNullOrWhiteSpace(inputHash)) return false;

            var rspRaw = string.IsNullOrWhiteSpace(rawQuery)
                ? GetResponseDataForSign_FromDecodedValues()
                : GetResponseDataForSign_FromRawQuery(rawQuery);

            var myChecksum = HmacSha512(secretKey, rspRaw);
            return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        // ===== Helpers =====

        private string HmacSha512(string key, string inputData)
        {
            if (key == null) key = "";
            if (inputData == null) inputData = "";

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hashValue = hmac.ComputeHash(inputBytes);
                var sb = new StringBuilder(hashValue.Length * 2);
                foreach (var b in hashValue)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Cách cũ: build lại từ _responseData (giá trị đã decode rồi encode lại) => có thể lệch
        private string GetResponseDataForSign_FromDecodedValues()
        {
            var data = new StringBuilder();

            if (_responseData.ContainsKey("vnp_SecureHashType"))
                _responseData.Remove("vnp_SecureHashType");

            if (_responseData.ContainsKey("vnp_SecureHash"))
                _responseData.Remove("vnp_SecureHash");

            foreach (var kv in _responseData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            if (data.Length > 0)
                data.Remove(data.Length - 1, 1);

            return data.ToString();
        }

        // Cách chuẩn: build từ raw query giữ nguyên encoded value từ VNPAY
        private string GetResponseDataForSign_FromRawQuery(string rawQuery)
        {
            // rawQuery dạng "?a=1&b=2"
            if (string.IsNullOrWhiteSpace(rawQuery))
                return GetResponseDataForSign_FromDecodedValues();

            if (rawQuery.StartsWith("?"))
                rawQuery = rawQuery.Substring(1);

            var dataList = new SortedList<string, string>(new VnPayCompare());

            var parts = rawQuery.Split('&');
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                var idx = part.IndexOf('=');
                var key = idx >= 0 ? part.Substring(0, idx) : part;
                var val = idx >= 0 ? part.Substring(idx + 1) : "";

                if (string.IsNullOrEmpty(key) || !key.StartsWith("vnp_")) continue;
                if (key == "vnp_SecureHash" || key == "vnp_SecureHashType") continue;

                dataList[key] = val; // giữ nguyên value đã encode
            }

            var sb = new StringBuilder();
            foreach (var kv in dataList)
            {
                sb.Append(kv.Key).Append("=").Append(kv.Value).Append("&");
            }
            if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var vnpCompare = CompareInfo.GetCompareInfo("en-US");
            return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
