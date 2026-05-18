using HinoTools.Alarm.Model.Event;
using System.ServiceModel;

namespace HinoTools.Alarm.Service.Event
{
    public class EventServiceClientBase : DuplexClientBase<IEventService>, IEventService
    {
        public EventServiceClientBase(InstanceContext callbackInstance) :
             base(callbackInstance)
        {
        }

        public EventServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName) :
                base(callbackInstance, endpointConfigurationName)
        {
        }

        public EventServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) :
                base(callbackInstance, endpointConfigurationName, remoteAddress)
        {
        }

        public EventServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName, EndpointAddress remoteAddress) :
                base(callbackInstance, endpointConfigurationName, remoteAddress)
        {
        }

        public EventServiceClientBase(InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) :
                base(callbackInstance, binding, remoteAddress)
        {
        }
        public void Connect()
        {
            base.Channel.Connect();
        }

        public void Disconnect()
        {
            base.Channel.Disconnect();
        }

        public bool Ping()
        {
            return base.Channel.Ping();
        }

        public EventItem[] GetItems(int maxCount)
        {
            return base.Channel.GetItems(maxCount);
        }  
        
        public void Push(EventItem eventItem)
        {
            base.Channel.Push(eventItem);
        }
    }
}
