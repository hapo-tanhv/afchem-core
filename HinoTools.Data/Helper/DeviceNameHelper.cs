using System;

namespace HinoTools.Data.Helper
{
    /// <summary>
    /// Tách tên máy từ tên tag ATSCADA.
    ///
    /// Đợt 3B: hàm này KHÔNG còn trả về "TX01" khi không phân giải được.
    /// Đoán tên máy nghĩa là gán dữ liệu sản xuất cho một máy có thật mà không ai biết,
    /// làm hỏng chuỗi truy xuất lô. Không phân giải được thì trả null và người gọi
    /// phải báo lỗi + từ chối ghi.
    ///
    /// Nguồn sự thật về máy là bảng `machines` — xem <see cref="MachineRegistry"/>.
    /// Hàm này chỉ dùng khi chưa có kết nối CSDL (ví dụ tách tiền tố để đi tra bảng).
    /// </summary>
    public static class DeviceNameHelper
    {
        /// <summary>
        /// Ví dụ: "AFChemTX01.CongDoan" -> "TX01"; "AFChemPLC" -> "PLC".
        /// Trả <c>null</c> khi tên tag rỗng hoặc không theo quy ước "AFChem&lt;tên máy&gt;".
        /// </summary>
        public static string ExtractDeviceName(string firstTagName)
        {
            string deviceName;
            string reason;
            return TryExtractDeviceName(firstTagName, out deviceName, out reason) ? deviceName : null;
        }

        /// <summary>
        /// Như <see cref="ExtractDeviceName"/> nhưng nêu rõ lý do thất bại để ghi cảnh báo.
        /// </summary>
        public static bool TryExtractDeviceName(string firstTagName, out string deviceName, out string reason)
        {
            deviceName = null;
            reason = null;

            if (string.IsNullOrEmpty(firstTagName))
            {
                reason = "Tên tag rỗng — không xác định được máy.";
                return false;
            }

            // Lấy phần trước dấu "."
            var dotIndex = firstTagName.IndexOf('.');
            string prefix = dotIndex > 0 ? firstTagName.Substring(0, dotIndex) : firstTagName;

            if (prefix.StartsWith("AFChem", StringComparison.OrdinalIgnoreCase) && prefix.Length > 6)
            {
                deviceName = prefix.Substring(6); // AFChemTX01 -> TX01
                return true;
            }

            reason = $"Tiền tố '{prefix}' của tag '{firstTagName}' không theo quy ước 'AFChem<tên máy>' — " +
                     $"không xác định được máy.";
            return false;
        }

        /// <summary>Tách tiền tố tag (ví dụ "AFChemTX01") để đi tra bảng machines. Null nếu rỗng.</summary>
        public static string ExtractTagPrefix(string firstTagName)
        {
            if (string.IsNullOrEmpty(firstTagName)) return null;

            var dotIndex = firstTagName.IndexOf('.');
            return dotIndex > 0 ? firstTagName.Substring(0, dotIndex) : firstTagName;
        }
    }
}
