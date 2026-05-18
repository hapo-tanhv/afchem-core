using System;
using System.Runtime.Serialization;

namespace HinoTools.Alarm.Model.Event
{
    [DataContract]
    public class EventItem : IComparable<EventItem>
    {
        [DataMember]
        public EventParam Param { get; set; }

        [DataMember]
        public EventStatus Status { get; set; }

        [DataMember]
        public DateTime OccurrenceTime { get; set; } = DateTime.MinValue;

        public EventItem() { }
        
        public EventItem(EventParam param, EventStatus status)
        {            
            Param = param;
            Status = status;
            OccurrenceTime = DateTime.Now;
        }       
        public int CompareTo(EventItem other)
        {
            return OccurrenceTime.CompareTo(other.OccurrenceTime);
        }
    }
}
