using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DoAnCoSo.Models.Libraries
{
    public class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayComparer());
        private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayComparer());

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _requestData.Add(key, value);
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _responseData.Add(key, value);
            }
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
        {
            StringBuilder data = new StringBuilder();
            StringBuilder rawData = new StringBuilder();
            // VnPayLibrary.cs - Trong hàm CreateRequestUrl
            foreach (KeyValuePair<string, string> kv in _requestData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    // 1. Dữ liệu nạp vào URL phải được Encode
                    data.Append(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&");

                    // 2. Dữ liệu dùng để băm chữ ký PHẢI dùng WebUtility.UrlEncode (chuẩn VNPAY 2.1.0)
                    // Lưu ý: Không dùng kv.Value trực tiếp nếu có dấu cách
                    rawData.Append(kv.Key + "=" + System.Net.WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            string queryString = data.ToString().TrimEnd('&');
            string signData = rawData.ToString().TrimEnd('&');

            // Tạo chữ ký SecureHash bằng thuật toán HMACSHA512
            string vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);

            return baseUrl + "?" + queryString + "&vnp_SecureHash=" + vnp_SecureHash;
        }

        public bool ValidateSignature(string inputHash, string secretKey)
        {
            StringBuilder data = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in _responseData)
            {
                if (!string.IsNullOrEmpty(kv.Value) && kv.Key.StartsWith("vnp_")
                    && kv.Key != "vnp_SecureHash"
                    && kv.Key != "vnp_SecureHashType")
                {
                  
                    data.Append(kv.Key + "=" + System.Net.WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            string rawData = data.ToString().TrimEnd('&');
            string checkSum = HmacSHA512(secretKey, rawData);

            return checkSum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var theByte in hashValue)
                {
                    hash.Append(theByte.ToString("X2")); // Sử dụng "x2" cho chuỗi hex thường (chuẩn VNPAY)
                }
            }
            return hash.ToString();
        }
    }

    public class VnPayComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // So sánh theo thứ tự Ordinal để đảm bảo chính xác bảng chữ cái cho các tham số VNPAY
            return string.CompareOrdinal(x, y);
        }
    }
}