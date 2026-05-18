using HinoTools.Alarm.Model;
using System.ServiceModel;

namespace HinoTools.Alarm.Service
{
    [ServiceBehavior(
        ConcurrencyMode = ConcurrencyMode.Single,
        InstanceContextMode = InstanceContextMode.PerCall)]
    public class AlarmServiceHostBase : IAlarmService
    {
        private readonly AlarmServiceDispatcher dispatcher;

        private readonly IAlarmHub alarmHub;
        
        public AlarmServiceHostBase()
        {
            this.dispatcher = AlarmServiceDispatcher.Instance;
            this.alarmHub = AlarmServiceDispatcher.Instance.AlarmHub;
        }

        #region SERVICE CONTRACTS

        public void Connect()
        {
            if (!this.dispatcher.IsActive) return;
            var callback = OperationContext.Current.GetCallbackChannel<IAlarmServiceCallback>();
            this.dispatcher.Register(callback);
        }

        public void Disconnect()
        {
            if (!this.dispatcher.IsActive) return;
            var callback = OperationContext.Current.GetCallbackChannel<IAlarmServiceCallback>();
            this.dispatcher.UnResgister(callback);
        }

        public bool Ping() => true;

        public AlarmItem[] GetItems(int maxCount)
        {
            if (!this.dispatcher.IsActive) return null;
            return this.alarmHub.GetItems(maxCount);
        }

        public void Acknowledge(string[] ids)
        {
            if (!this.dispatcher.IsActive) return;
            this.alarmHub.Acknowledge(ids);
        }

        public void Reset()
        {
            if (!this.dispatcher.IsActive) return;
            this.alarmHub.Reset();
        }

        #endregion       
    }
}
