using System.Runtime.Serialization;

namespace HinoTools.Alarm.Model
{
    [DataContract]
    public class AlarmParam
    {
        [DataMember]
        public string TagName { get; set; }

        [DataMember]
        public string TagNo { get; set; }

        [DataMember]
        public string Location { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public AlarmLevel Level { get; set; }

        public string Value { get; set; }

        public AlarmType Type { get; set; }

        [DataMember]
        public int FaultCode { get; set; }
    }
}
