using HinoTools.Alarm.Model;
using System.ServiceModel;

namespace HinoTools.Alarm.Service
{
    public class AlarmServiceClientBase : DuplexClientBase<IAlarmService>, IAlarmService
    {
        public AlarmServiceClientBase(InstanceContext callbackInstance) :
             base(callbackInstance)
        {            
        }

        public AlarmServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName) :
                base(callbackInstance, endpointConfigurationName)
        {
        }

        public AlarmServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) :
                base(callbackInstance, endpointConfigurationName, remoteAddress)
        {
        }

        public AlarmServiceClientBase(InstanceContext callbackInstance, string endpointConfigurationName, EndpointAddress remoteAddress) :
                base(callbackInstance, endpointConfigurationName, remoteAddress)
        {
        }

        public AlarmServiceClientBase(InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) :
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

        public AlarmItem[] GetItems(int maxCount)
        {
            return base.Channel.GetItems(maxCount);
        }

        public void Acknowledge(string[] ids)
        {
            base.Channel.Acknowledge(ids);
        }

        public void Reset()
        {
            base.Channel.Reset();
        }        
    }
}
