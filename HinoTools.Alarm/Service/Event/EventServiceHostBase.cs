using HinoTools.Alarm.Model.Event;
using System.ServiceModel;

namespace HinoTools.Alarm.Service.Event
{
    [ServiceBehavior(
        ConcurrencyMode = ConcurrencyMode.Single,
        InstanceContextMode = InstanceContextMode.PerCall)]
    public class EventServiceHostBase : IEventService
    {
        private readonly EventServiceDispatcher dispatcher;

        private readonly IEventHub eventHub;

        public EventServiceHostBase()
        {
            this.dispatcher = EventServiceDispatcher.Instance;
            this.eventHub = EventServiceDispatcher.Instance.EventHub;
        }

        #region SERVICE CONTRACTS

        public void Connect()
        {
            if (!this.dispatcher.IsActive) return;
            var callback = OperationContext.Current.GetCallbackChannel<IEventServiceCallback>();
            if (callback is null) return;
            this.dispatcher.Register(callback);
        }

        public void Disconnect()
        {
            if (!this.dispatcher.IsActive) return;
            var callback = OperationContext.Current.GetCallbackChannel<IEventServiceCallback>();
            if (callback is null) return;
            this.dispatcher.UnResgister(callback);
        }

        public bool Ping() => true;

        public EventItem[] GetItems(int maxCount)
        {
            if (!this.dispatcher.IsActive) return null;
            return this.eventHub.GetItems(maxCount);
        }

        public void Push(EventItem eventItem)
        {
            if (!this.dispatcher.IsActive) return;
            this.eventHub.Push(eventItem);
        }

        #endregion
    }
}
