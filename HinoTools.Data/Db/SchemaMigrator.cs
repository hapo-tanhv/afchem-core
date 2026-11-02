using MySql.Data.MySqlClient;
using System;

namespace HinoTools.Data.Db
{
    /// <summary>
    /// Đợt 3B: các thay đổi schema của đợt này gom về MỘT chỗ.
    ///
    /// Trước đây mỗi thành phần tự vá schema theo cách riêng, dẫn tới việc
    /// một cột chỉ tồn tại nếu đúng thành phần đó chạy trước — Read API từng lỗi 500
    /// trên CSDL mới vì cột official_lot_code chỉ được webhook tạo.
    ///
    /// Hàm ở đây phải idempotent (chạy lại nhiều lần vẫn đúng) và CHỈ THÊM,
    /// không bao giờ sửa/xoá dữ liệu đã ghi.
    /// </summary>
    public static class SchemaMigrator
    {
        /// <summary>
        /// Đảm bảo đủ schema Đợt 3B: bảng danh mục máy + cột mã lô chính thức.
        /// Gọi được từ bất kỳ thành phần nào ngay sau khi mở kết nối.
        /// </summary>
        public static void EnsureDot3BSchema(MySqlConnection conn)
        {
            EnsureMachinesTable(conn);
            EnsureOfficialLotCodeColumn(conn);
            EnsureProductLotCodeColumn(conn);
        }

        private static void EnsureMachinesTable(MySqlConnection conn)
        {
            try
            {
                Helper.MachineRegistry.EnsureTable(conn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SchemaMigrator] LỖI tạo bảng machines: {ex.Message}");
            }
        }

        private static void EnsureOfficialLotCodeColumn(MySqlConnection conn)
        {
            try
            {
                if (ColumnExists(conn, "batches", "official_lot_code")) return;

                using (var cmd = new MySqlCommand(
                    "ALTER TABLE `batches` ADD COLUMN `official_lot_code` VARCHAR(100) NULL AFTER `name`", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                System.Diagnostics.Debug.WriteLine("[SchemaMigrator] Đã thêm cột batches.official_lot_code.");

                // Chỉ mục để tra theo mã lô (đường tra chính của MES).
                // KHÔNG dùng UNIQUE: dữ liệu lịch sử có nhiều lô cùng NULL, và ràng buộc duy nhất
                // được thực thi ở tầng ứng dụng khi tạo lô mới.
                try
                {
                    using (var cmd = new MySqlCommand(
                        "CREATE INDEX `idx_batches_official_lot` ON `batches` (`official_lot_code`)", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SchemaMigrator] Không tạo được chỉ mục official_lot_code: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SchemaMigrator] LỖI thêm cột official_lot_code: {ex.Message}");
            }
        }

        /// <summary>
        /// Mã lô thành phẩm từ MES (AFC-FG-…), tách khỏi official_lot_code là mã mẻ BAT-.
        /// Khớp db/migrations/2026-11-03-mes-dispatch.sql.
        /// </summary>
        private static void EnsureProductLotCodeColumn(MySqlConnection conn)
        {
            try
            {
                if (ColumnExists(conn, "batches", "product_lot_code")) return;

                using (var cmd = new MySqlCommand(
                    "ALTER TABLE `batches` ADD COLUMN `product_lot_code` VARCHAR(100) NULL " +
                    "COMMENT 'Mã lô thành phẩm từ MES' AFTER `official_lot_code`", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                System.Diagnostics.Debug.WriteLine("[SchemaMigrator] Đã thêm cột batches.product_lot_code.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SchemaMigrator] LỖI thêm cột product_lot_code: {ex.Message}");
            }
        }

        private static bool ColumnExists(MySqlConnection conn, string table, string column)
        {
            using (var cmd = new MySqlCommand($"SHOW COLUMNS FROM `{table}` LIKE @col", conn))
            {
                cmd.Parameters.AddWithValue("@col", column);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }
    }
}
