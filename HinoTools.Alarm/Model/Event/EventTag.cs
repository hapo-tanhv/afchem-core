using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using System;

namespace HinoTools.Alarm.Model.Event
{
    public static class EventTagFactory
    {
        public static EventTagBase Get(iDriver driver, EventParam param)
        {
            switch (param.Type)
            {
                case EventType.Changed: return new EventChangedTag(driver, param);
                case EventType.Write: return new EventWriteTag(driver, param);
                default: return new EventTag(driver, param);
            }
        }
    }

    public abstract class EventTagBase
    {
        #region FIELDS

        protected readonly ITag tag;

        private volatile bool isBusy;

        private DateTime timeStamp;
        
        #endregion

        #region PROPERTIES

        public bool IsActive { get; protected set; }

        public EventStatus Status { get; protected set; }

        public EventParam Param { get; protected set; }

        public event Action<EventItem> StatusChanged;

        #endregion

        public EventTagBase(iDriver driver, EventParam param)
        {
            Param = param;
            this.tag = driver?.GetTagByName(Param.TagName);
            if (this.tag is null) 
                return;

            IsActive = true;
            this.tag.TagValueChanged += TagValueChanged;
            this.tag.TagStatusChanged += TagStatusChanged;
            CheckEvent(this.tag.Value, false);
        }

        private void TagValueChanged(object sender, TagValueEventArgs e)
        {
            if (this.isBusy) return;
            this.timeStamp = DateTime.Now;
            CheckEvent(true);            
        }

        private  void TagStatusChanged(object sender, TagStatusEventArgs e)
        {
            if (this.isBusy) return;
            this.timeStamp = DateTime.Now;
            CheckEvent(true);            
        }

        protected async void CheckEvent(bool allowNotify)
        {
            if (this.tag.Status != "Good") return;
            var value = this.tag.Value;
            if (string.IsNullOrEmpty(this.tag.Value)) return;
            try
            {                
                this.isBusy = true;
                await System.Threading.Tasks.Task.Delay(5000);
                if (this.tag.Status != "Good") return;
                if (this.tag.Value == value)
                {
                    this.isBusy = false;
                    CheckEvent(value, allowNotify);
                }                               
            }
            catch { }
            finally
            {
                this.isBusy = false;
            }
        }

        protected abstract void CheckEvent(string value, bool allowNotify);

        protected abstract void OnEvent(bool allowNotify);


        protected abstract void OffEvent(bool allowNotify);

        protected void OnEventStatusChanged(EventParam param, EventStatus status)
        {
            Action<EventItem> handler;
            lock (this) handler = StatusChanged;
            handler?.Invoke(new EventItem(param, status) 
            {
                OccurrenceTime = this.timeStamp
            });
        }
    }

    public class EventTag : EventTagBase
    {
        public EventTag(iDriver driver, EventParam param) : base(driver, param)
        {
        }

        protected override void CheckEvent(string value, bool allowNotify)
        {
            if (string.Equals(value.Trim(), Param.Value))
            {
                OnEvent(allowNotify);
                return;
            }
            OffEvent(allowNotify);
        }

        protected override void OnEvent(bool allowNotify)
        {
            if (Status == EventStatus.ACTIVE) return;
            Status = EventStatus.ACTIVE;
            if (allowNotify && Param.Type != EventType.Off)
                OnEventStatusChanged(Param, Status);
        }

        protected override void OffEvent(bool allowNotify)
        {
            if (Status == EventStatus.INACTIVE) return;
            Status = EventStatus.INACTIVE;
            if (allowNotify && Param.Type != EventType.On)
                OnEventStatusChanged(Param, Status);
        }
    }

    public class EventChangedTag : EventTagBase
    {
        protected string oldValue;

        public EventChangedTag(iDriver driver, EventParam param) : base(driver, param)
        {
        }

        protected override void CheckEvent(string value, bool allowNotify)
        {
            if (value != this.oldValue)
            {

                var newParam = new EventParam(Param);
                newParam.Description = string.Format($"{Param.Description}", this.oldValue, value);
                this.oldValue = value;
                if (allowNotify)
                    OnEventStatusChanged(newParam, EventStatus.CHANGED);
            }
        }

        protected override void OnEvent(bool allowNotify)
        {
            return;
        }

        protected override void OffEvent(bool allowNotify)
        {
            return;
        }
    }

    public class EventWriteTag : EventTagBase
    {
        public EventWriteTag(iDriver driver, EventParam param) : base(driver, param)
        {
            this.tag.WriteStatusFeedback += Tag_WriteStatusFeedback;
        }

        private void Tag_WriteStatusFeedback(object o, TagStatusEventArgs e)
        {
            if (e.NewStatus == "Good")
            {
                var newParam = new EventParam(Param);
                newParam.Description = string.Format($"{Param.Description}", this.tag.ValuetoWrite);
                OnEventStatusChanged(newParam, EventStatus.CHANGED);
            }
        }

        protected override void CheckEvent(string value, bool allowNotify)
        {
            return;
        }

        protected override void OnEvent(bool allowNotify)
        {
            return;
        }

        protected override void OffEvent(bool allowNotify)
        {
            return;
        }
    }
}
