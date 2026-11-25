using System;

namespace HinoTools.Data.Helper
{
    /// <summary>
    /// Đợt 3B: định dạng thời gian cho dữ liệu trao đổi với hệ ngoài (MES/ERP).
    ///
    /// Cột DATETIME trong CSDL không mang thông tin múi giờ và đang lưu theo giờ
    /// Việt Nam (Asia/Ho_Chi_Minh, UTC+07:00). Khi đẩy ra ngoài phải ghi kèm offset,
    /// nếu không hệ nhận sẽ tự hiểu là UTC và lệch 7 tiếng — chuỗi truy xuất lệch giờ
    /// nghĩa là truy ngược sai lô.
    ///
    /// Cố ý dùng offset cố định +07:00 thay vì offset của máy đang chạy: dữ liệu trong DB
    /// là giờ Việt Nam bất kể máy chạy code đặt ở đâu.
    /// </summary>
    public static class TimeFormat
    {
        /// <summary>Offset của Asia/Ho_Chi_Minh. Việt Nam không áp dụng giờ mùa hè.</summary>
        public const string VietnamOffset = "+07:00";

        public const string Iso8601Pattern = "yyyy-MM-dd'T'HH:mm:ss";

        /// <summary>Chuyển DATETIME đọc từ DB thành chuỗi ISO 8601 kèm offset +07:00.</summary>
        public static string ToIso8601(DateTime value)
        {
            return value.ToString(Iso8601Pattern) + VietnamOffset;
        }

        /// <summary>Như <see cref="ToIso8601"/>, trả null cho giá trị rỗng.</summary>
        public static string ToIso8601OrNull(DateTime? value)
        {
            return value.HasValue ? ToIso8601(value.Value) : null;
        }

        /// <summary>Chuỗi JSON: giá trị ISO trong nháy kép, hoặc literal null.</summary>
        public static string ToJsonValue(DateTime? value)
        {
            return value.HasValue ? "\"" + ToIso8601(value.Value) + "\"" : "null";
        }
    }
}
