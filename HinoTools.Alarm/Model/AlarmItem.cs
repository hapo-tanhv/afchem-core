using System;
using System.Runtime.Serialization;

namespace HinoTools.Alarm.Model
{
    [DataContract]
    public class AlarmItem : IComparable<AlarmItem>
    {               
        [DataMember]
        public string ID { get; set; }

        [DataMember]
        public bool IsAcknowledge { get; set; }

        [DataMember]
        public AlarmParam Param { get; set; }

        [DataMember]
        public AlarmStatus Status { get; set; }

        [DataMember]
        public DateTime OccurrenceTime { get; set; } = DateTime.MinValue;

        [DataMember]
        public DateTime RestoreTime { get; set; } = DateTime.MinValue;       
       
        public int CompareTo(AlarmItem other)
        {
            return OccurrenceTime.CompareTo(other.OccurrenceTime);
        }
    }    

}
