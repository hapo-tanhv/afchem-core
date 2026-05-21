using System;

namespace HinoTools.Data.Helper
{
    public static class DeviceNameHelper
    {
        /// <summary>
        /// Extracts the device name dynamically by stripping ATSCADA dot-notation and "AFChem" prefixes.
        /// Example: "AFChemTX01.CongDoan" -> "TX01"
        /// "AFChemPLC" -> "PLC"
        /// </summary>
        public static string ExtractDeviceName(string firstTagName)
        {
            if (string.IsNullOrEmpty(firstTagName)) return "TX01";

            // Extract the part before the dot "."
            var dotIndex = firstTagName.IndexOf('.');
            string prefix = dotIndex > 0 ? firstTagName.Substring(0, dotIndex) : firstTagName;

            // Remove the "AFChem" prefix if it exists
            if (prefix.StartsWith("AFChem", StringComparison.OrdinalIgnoreCase) && prefix.Length > 6)
            {
                return prefix.Substring(6); // e.g. AFChemTX01 -> TX01, AFChemPLC -> PLC
            }

            return prefix;
        }
    }
}
