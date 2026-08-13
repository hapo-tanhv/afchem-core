using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HinoTools.Data.Helper
{
    /// <summary>
    /// Đợt 3B: danh mục máy nạp từ bảng <c>machines</c>.
    ///
    /// Thay cho việc bóc tên máy từ chuỗi tag rồi âm thầm gán "TX01" khi không khớp,
    /// mọi chỗ cần biết "bản ghi này của máy nào" đều phải tra qua đây, và
    /// KHÔNG khớp thì phải báo lỗi to + từ chối ghi (fail-loud).
    ///
    /// SCADA vẫn là tầng thiết bị: bảng này do người quản trị dữ liệu nạp từ danh mục
    /// máy của Core, SCADA không gọi Core API.
    /// </summary>
    public static class MachineRegistry
    {
        private static readonly object cacheLock = new object();
        private static List<MachineInfo> cache;
        private static DateTime cacheLoadedAt = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        /// <summary>Xoá cache, buộc đọc lại bảng ở lần tra kế tiếp.</summary>
        public static void Invalidate()
        {
            lock (cacheLock)
            {
                cache = null;
                cacheLoadedAt = DateTime.MinValue;
            }
        }

        /// <summary>Danh sách máy đang hoạt động.</summary>
        public static IList<MachineInfo> ActiveMachines(string connectionString)
        {
            return Load(connectionString).Where(m => m.Active).ToList();
        }

        /// <summary>
        /// Tra máy theo tên tag đầy đủ, ví dụ "AFChemTX01.NhietDoMay" -> máy có tag_prefix "AFChemTX01".
        /// Trả false kèm lý do khi không tra được — người gọi PHẢI xử lý, không được gán mặc định.
        /// </summary>
        public static bool TryResolveByTag(string connectionString, string fullTagName,
                                           out MachineInfo machine, out string reason)
        {
            machine = null;
            reason = null;

            if (string.IsNullOrEmpty(fullTagName))
            {
                reason = "Tên tag rỗng — không xác định được máy.";
                return false;
            }

            int dotIndex = fullTagName.IndexOf('.');
            string prefix = dotIndex > 0 ? fullTagName.Substring(0, dotIndex) : fullTagName;

            List<MachineInfo> machines;
            try
            {
                machines = Load(connectionString);
            }
            catch (Exception ex)
            {
                reason = "Không đọc được bảng machines: " + ex.Message;
                return false;
            }

            var found = machines.FirstOrDefault(m =>
                string.Equals(m.TagPrefix, prefix, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                reason = $"Tag '{fullTagName}' (tiền tố '{prefix}') không khớp máy nào trong bảng machines. " +
                         $"Các tiền tố hợp lệ: {DescribeKnownPrefixes(machines)}.";
                return false;
            }

            if (!found.Active)
            {
                reason = $"Máy '{found.DeviceName}' (tiền tố '{prefix}') đang bị đánh dấu ngừng hoạt động trong bảng machines.";
                return false;
            }

            machine = found;
            return true;
        }

        /// <summary>Tra máy theo tên thiết bị kỹ thuật (device_name), ví dụ "TX01".</summary>
        public static bool TryResolveByDeviceName(string connectionString, string deviceName,
                                                  out MachineInfo machine, out string reason)
        {
            machine = null;
            reason = null;

            if (string.IsNullOrEmpty(deviceName))
            {
                reason = "Thiếu tên thiết bị — hệ không tự đoán máy.";
                return false;
            }

            List<MachineInfo> machines;
            try
            {
                machines = Load(connectionString);
            }
            catch (Exception ex)
            {
                reason = "Không đọc được bảng machines: " + ex.Message;
                return false;
            }

            var found = machines.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                reason = $"Thiết bị '{deviceName}' không có trong bảng machines. " +
                         $"Các máy hợp lệ: {DescribeKnownDevices(machines)}.";
                return false;
            }

            if (!found.Active)
            {
                reason = $"Máy '{deviceName}' đang bị đánh dấu ngừng hoạt động trong bảng machines.";
                return false;
            }

            machine = found;
            return true;
        }

        /// <summary>Kiểm tra nhanh một device_name có hợp lệ không (dùng cho tầng HTTP).</summary>
        public static bool IsKnownDevice(string connectionString, string deviceName, out string error)
        {
            MachineInfo machine;
            return TryResolveByDeviceName(connectionString, deviceName, out machine, out error);
        }

        /// <summary>
        /// Tạo bảng machines nếu chưa có và seed máy TX01 đang chạy (mc_code để trống,
        /// chờ Core cấp mã MC-). Theo đúng cách tự vá schema mà các thành phần khác đang dùng.
        /// </summary>
        public static void EnsureTable(MySqlConnection conn)
        {
            const string createSql =
                "CREATE TABLE IF NOT EXISTS `machines` (" +
                "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                "  `mc_code` VARCHAR(50) NULL," +
                "  `device_name` VARCHAR(100) NOT NULL," +
                "  `tag_prefix` VARCHAR(100) NOT NULL," +
                "  `active` TINYINT(1) NOT NULL DEFAULT 1," +
                "  `note` VARCHAR(255) NULL," +
                "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                "  UNIQUE KEY `uq_machines_device` (`device_name`)," +
                "  UNIQUE KEY `uq_machines_tag_prefix` (`tag_prefix`)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            using (var cmd = new MySqlCommand(createSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Seed máy đang vận hành để hệ không dừng ghi ngay khi lên phiên bản mới.
            // mc_code để NULL: mã MC- do Core cấp, SCADA không được tự bịa.
            const string seedSql =
                "INSERT INTO `machines` (`mc_code`, `device_name`, `tag_prefix`, `active`, `note`) " +
                "SELECT NULL, 'TX01', 'AFChemTX01', 1, 'CHỜ MÃ MC- TỪ CORE' FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM `machines` WHERE `device_name` = 'TX01')";

            using (var cmd = new MySqlCommand(seedSql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Danh sách máy chưa được Core cấp mã MC- (để cảnh báo khi khởi động).</summary>
        public static IList<MachineInfo> MachinesWithoutMcCode(string connectionString)
        {
            return Load(connectionString).Where(m => m.Active && !m.HasMcCode).ToList();
        }

        private static List<MachineInfo> Load(string connectionString)
        {
            lock (cacheLock)
            {
                if (cache != null && (DateTime.Now - cacheLoadedAt) < CacheTtl)
                {
                    return cache;
                }
            }

            var loaded = new List<MachineInfo>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                EnsureTable(conn);

                using (var cmd = new MySqlCommand(
                    "SELECT `mc_code`, `device_name`, `tag_prefix`, `active` FROM `machines`", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        loaded.Add(new MachineInfo
                        {
                            McCode = reader.IsDBNull(0) ? null : reader.GetString(0),
                            DeviceName = reader.GetString(1),
                            TagPrefix = reader.GetString(2),
                            Active = !reader.IsDBNull(3) && reader.GetInt32(3) != 0
                        });
                    }
                }
            }

            lock (cacheLock)
            {
                cache = loaded;
                cacheLoadedAt = DateTime.Now;
                return cache;
            }
        }

        private static string DescribeKnownPrefixes(List<MachineInfo> machines)
        {
            var active = machines.Where(m => m.Active).Select(m => m.TagPrefix).ToList();
            return active.Count == 0 ? "(bảng machines đang rỗng)" : string.Join(", ", active);
        }

        private static string DescribeKnownDevices(List<MachineInfo> machines)
        {
            var active = machines.Where(m => m.Active).Select(m => m.DeviceName).ToList();
            return active.Count == 0 ? "(bảng machines đang rỗng)" : string.Join(", ", active);
        }
    }
}
