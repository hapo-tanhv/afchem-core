using System.Runtime.Serialization;

namespace HinoTools.Alarm.Model.Event
{
    [DataContract]
    public class EventParam
    {
        [DataMember]
        public string TagName { get; set; } = "";

        [DataMember]
        public string TagNo { get; set; } = "";

        [DataMember]
        public string Value { get; set; } = "";

        [DataMember]
        public string ValueActive { get; set; } = "CUSTOM";

        [DataMember]
        public string ValueInactive { get; set; } = "CUSTOM";

        [DataMember]
        public string Location { get; set; } = "";

        [DataMember]
        public string Description { get; set; } = "";

        [DataMember]
        public EventType Type { get; set; }

        [DataMember]
        public int Level { get; set; }

        public EventParam() { }

        public EventParam(EventParam param)
        {
            TagName = param.TagName;
            TagNo = param.TagNo;
            Value = param.Value;
            ValueActive = param.ValueActive;
            ValueInactive = param.ValueInactive;
            Location = param.Location;
            Description = param.Description;
            Type = param.Type;
            Level = param.Level;
        }
    }
}
