namespace HinoTools.Data.Helper
{
    /// <summary>
    /// Một máy sản xuất trong danh mục máy (bảng <c>machines</c>).
    ///
    /// Đợt 3B: SCADA không tự đặt mã máy. Mã chính thức là <see cref="McCode"/> (dạng MC-)
    /// do Core cấp, nạp vào bảng này; <see cref="DeviceName"/> chỉ là tên kỹ thuật cũ
    /// (TX01) dùng cho dữ liệu lịch sử và tên tag.
    /// </summary>
    public class MachineInfo
    {
        /// <summary>Mã máy chính thức từ Core, dạng "MC-...". NULL/rỗng khi Core chưa cấp.</summary>
        public string McCode { get; set; }

        /// <summary>Tên thiết bị kỹ thuật, ví dụ "TX01". Là giá trị đang lưu ở batches.device_name.</summary>
        public string DeviceName { get; set; }

        /// <summary>Tiền tố tên tag ATSCADA, ví dụ "AFChemTX01".</summary>
        public string TagPrefix { get; set; }

        public bool Active { get; set; }

        /// <summary>True khi Core đã cấp mã MC- cho máy này.</summary>
        public bool HasMcCode
        {
            get { return !string.IsNullOrEmpty(McCode); }
        }

        /// <summary>Nhãn hiển thị: ưu tiên mã chính thức, chưa có thì dùng tên kỹ thuật.</summary>
        public string DisplayCode
        {
            get { return HasMcCode ? McCode : DeviceName; }
        }
    }
}
