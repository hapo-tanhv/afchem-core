using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace HinoTools.Data.Config
{
    /// <summary>
    /// Đọc cấu hình runtime (thông tin kết nối MySQL, token API) từ file
    /// <c>scada-runtime.config</c> đặt cạnh file thực thi, thay cho việc
    /// nhúng mật khẩu/token thẳng trong mã nguồn.
    ///
    /// Thứ tự ưu tiên khi phân giải một giá trị (xem <see cref="Resolve"/>):
    ///   1. Giá trị đặt tường minh trên component (ATSCADA designer) — để hệ đang chạy không bị gián đoạn.
    ///   2. Giá trị trong scada-runtime.config.
    ///   3. Không có cả hai  ->  ném lỗi nêu đúng tên khoá còn thiếu (fail-loud, không đoán ngầm).
    ///
    /// Đường dẫn file có thể ghi đè bằng biến môi trường SCADA_RUNTIME_CONFIG
    /// (dùng cho máy staging / test host).
    /// </summary>
    public static class ScadaConfig
    {
        public const string FileName = "scada-runtime.config";
        public const string PathEnvironmentVariable = "SCADA_RUNTIME_CONFIG";

        public const string KeyDbServer = "Db.Server";
        public const string KeyDbUser = "Db.User";
        public const string KeyDbPassword = "Db.Password";
        public const string KeyDbName = "Db.Name";
        public const string KeyWebhookToken = "Api.WebhookToken";
        public const string KeyReadApiToken = "Api.ReadApiToken";

        private static readonly object loadLock = new object();
        private static Dictionary<string, string> values;
        private static string loadedFrom;

        /// <summary>Đường dẫn file cấu hình đang dùng (rỗng nếu không tìm thấy).</summary>
        public static string ConfigFilePath
        {
            get { EnsureLoaded(); return loadedFrom ?? ""; }
        }

        /// <summary>True khi tìm và đọc được file cấu hình.</summary>
        public static bool FileExists
        {
            get { EnsureLoaded(); return !string.IsNullOrEmpty(loadedFrom); }
        }

        public static string Server { get { return Get(KeyDbServer); } }
        public static string User { get { return Get(KeyDbUser); } }
        public static string Password { get { return Get(KeyDbPassword); } }
        public static string Database { get { return Get(KeyDbName); } }
        public static string WebhookToken { get { return Get(KeyWebhookToken); } }
        public static string ReadApiToken { get { return Get(KeyReadApiToken); } }

        /// <summary>Lấy giá trị theo khoá; trả chuỗi rỗng nếu không có.</summary>
        public static string Get(string key)
        {
            EnsureLoaded();
            string value;
            return values.TryGetValue(key, out value) ? value : "";
        }

        /// <summary>
        /// Phân giải một giá trị BẮT BUỘC. Dùng cho thông tin nhạy cảm (user, mật khẩu, token):
        /// không có giá trị hợp lệ thì ném lỗi thay vì âm thầm dùng mặc định.
        /// </summary>
        public static string Resolve(string explicitValue, string configKey)
        {
            if (!string.IsNullOrEmpty(explicitValue)) return explicitValue;

            string fromConfig = Get(configKey);
            if (!string.IsNullOrEmpty(fromConfig)) return fromConfig;

            throw new InvalidOperationException(
                "Thiếu cấu hình bắt buộc '" + configKey + "'. " +
                "Hãy đặt giá trị này trong file " + FileName +
                (FileExists ? " (" + loadedFrom + ")" : " (chưa tìm thấy file cấu hình)") +
                ", hoặc đặt trực tiếp trên component trong ATSCADA.");
        }

        /// <summary>
        /// Phân giải một giá trị KHÔNG nhạy cảm (tên máy chủ, tên database):
        /// cho phép rơi về giá trị mặc định công khai.
        /// </summary>
        public static string ResolveOptional(string explicitValue, string configKey, string fallback)
        {
            if (!string.IsNullOrEmpty(explicitValue)) return explicitValue;

            string fromConfig = Get(configKey);
            if (!string.IsNullOrEmpty(fromConfig)) return fromConfig;

            return fallback;
        }

        /// <summary>Buộc nạp lại ở lần truy cập kế tiếp (dùng khi đổi file cấu hình lúc chạy).</summary>
        public static void Invalidate()
        {
            lock (loadLock)
            {
                values = null;
                loadedFrom = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (values != null) return;

            lock (loadLock)
            {
                if (values != null) return;

                var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string path = ResolveConfigPath();

                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        var doc = XDocument.Load(path);
                        var appSettings = doc.Root == null ? null : doc.Root.Element("appSettings");
                        if (appSettings != null)
                        {
                            foreach (var add in appSettings.Elements("add"))
                            {
                                var keyAttr = add.Attribute("key");
                                var valueAttr = add.Attribute("value");
                                if (keyAttr != null && valueAttr != null)
                                {
                                    loaded[keyAttr.Value.Trim()] = valueAttr.Value;
                                }
                            }
                        }
                        loadedFrom = path;
                    }
                    catch (Exception ex)
                    {
                        // Không nuốt lỗi: file có nhưng hỏng là tình huống phải biết ngay.
                        System.Diagnostics.Debug.WriteLine(
                            "[ScadaConfig] LỖI đọc file cấu hình '" + path + "': " + ex.Message);
                        loadedFrom = null;
                    }
                }

                values = loaded;
            }
        }

        private static string ResolveConfigPath()
        {
            try
            {
                string overridePath = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
                if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                {
                    return overridePath;
                }

                string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", FileName);
                if (File.Exists(beside)) return beside;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[ScadaConfig] LỖI xác định đường dẫn cấu hình: " + ex.Message);
            }

            return null;
        }
    }
}
