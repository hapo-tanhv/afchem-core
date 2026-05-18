using ATSCADA;

namespace HinoTools.Data.Log
{
    public class LogItem
    {
        public bool IsActive => Tag != null;

        public ITag Tag { get; set; }

        public string TagName { get; set; }

        public string Alias { get; set; }
    }
}
