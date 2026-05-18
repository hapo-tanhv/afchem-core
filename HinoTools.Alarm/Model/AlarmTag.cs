using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using System;

namespace HinoTools.Alarm.Model
{
    public static class AlarmTagFactory
    {
        public static AlarmTagBase Get(iDriver driver, AlarmParam param)
        {
            switch (param.Type)
            {
                case AlarmType.Bit: return new BitAlarmTag(driver, param);
                case AlarmType.Value: return new ValueAlarmTag(driver, param);
                case AlarmType.Continuous: return new ContinuousAlarmTag(driver, param);
                default: return new ValueAlarmTag(driver, param);
            }
        }
    }

    public abstract class AlarmTagBase
    {
        #region FIELDS

        protected readonly ITag tag;

        protected string lastAlarmItemID = Guid.NewGuid().ToString();

        protected DateTime lastOccurrenceTime = DateTime.MinValue;

        private volatile bool isBusy;

        private DateTime timeStamp;

        #endregion

        #region PROPERTIES

        public bool IsActive { get; private set; }

        public AlarmStatus Status { get; private set; }

        public AlarmParam Param { get; private set; }

        public event Action<AlarmItem> StatusChanged;

        #endregion

        public AlarmTagBase(iDriver driver, AlarmParam param)
        {
            Param = param;
            this.tag = driver?.GetTagByName(Param.TagName);
            if (this.tag is null) 
                return;

            IsActive = true;
            this.tag.TagValueChanged += TagValueChanged;
            this.tag.TagStatusChanged += TagStatusChanged;
            CheckAlarm(false);
        }

        private void TagValueChanged(object sender, TagValueEventArgs e)
        {
            if (this.isBusy) return;
            this.timeStamp = DateTime.Now;
            CheckAlarm(true);
        }

        private void TagStatusChanged(object sender, TagStatusEventArgs e)
        {
            if (this.isBusy) return;
            this.timeStamp = DateTime.Now;
            CheckAlarm(true);
        }

        protected async void CheckAlarm(bool allowNotifyAlarm)
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
                    CheckCondition(value, allowNotifyAlarm);
                }                
            }
            catch { }
            finally
            {
            }
        }

        protected abstract void CheckCondition(string value, bool allowNotifyAlarm);

        protected void OnAlarm(bool allowNotifyAlarm)
        {
            if (Status == AlarmStatus.ALARM) return;
            Status = AlarmStatus.ALARM;
            if (allowNotifyAlarm)
            {
                this.lastAlarmItemID = Guid.NewGuid().ToString();
                //this.lastOccurrenceTime = DateTime.Now;
                this.lastOccurrenceTime = this.timeStamp;

                OnAlarmStatusChanged(new AlarmItem()
                {
                    ID = this.lastAlarmItemID,
                    Param = Param,
                    Status = AlarmStatus.ALARM,
                    OccurrenceTime = this.lastOccurrenceTime
                });
            }
        }

        protected void OffAlarm(bool allowNotifyAlarm)
        {
            if (Status == AlarmStatus.NORMAL) return;
            Status = AlarmStatus.NORMAL;
            if (allowNotifyAlarm)
            {
                OnAlarmStatusChanged(new AlarmItem()
                {
                    ID = this.lastAlarmItemID,
                    Param = Param,
                    Status = AlarmStatus.NORMAL,
                    OccurrenceTime = this.lastOccurrenceTime,
                    RestoreTime = this.timeStamp
                    //RestoreTime = DateTime.Now
                });
            }
        }

        protected void OnAlarmStatusChanged(AlarmItem item)
        {
            Action<AlarmItem> handler;
            lock (this) handler = StatusChanged;
            handler?.Invoke(item);
        }
    }

    public class BitAlarmTag : AlarmTagBase
    {
        private const int ActiceValue = 1;

        private readonly byte bitIndex;

        public BitAlarmTag(iDriver driver, AlarmParam param) : base(driver, param)
        {
            byte.TryParse(param.Value, out bitIndex);
        }

        protected override async void CheckCondition(string value, bool allowNotifyAlarm)
        {
            if (this.tag.Status != "Good") return;
            if (this.tag.Value != value) return;

            if (!ushort.TryParse(value, out ushort valueParse)) return;
            if (BitCount(valueParse) > 5)
            {
                await System.Threading.Tasks.Task.Delay(10000);
                if (this.tag.Status != "Good") return;
                if (this.tag.Value != value) return;
            }

            var bitValue = (valueParse >> bitIndex) & 1;
            if (bitValue == ActiceValue)
            {
                OnAlarm(allowNotifyAlarm);
                return;
            }
            OffAlarm(allowNotifyAlarm);
        }

        private int BitCount(ushort value)
        {
            var count = 0;
            while (value != 0)
            {
                count++;
                value &= (ushort)(value - 1);
            }

            return count;
        }
    }

    public class ValueAlarmTag : AlarmTagBase
    {
        public ValueAlarmTag(iDriver driver, AlarmParam param) : base(driver, param)
        {
        }

        protected override async void CheckCondition(string value, bool allowNotifyAlarm)
        {
            await System.Threading.Tasks.Task.Delay(5000);
            if (this.tag.Status != "Good") return;
            if (this.tag.Value != value) return;

            if (string.Equals(value, Param.Value))
            {
                OnAlarm(allowNotifyAlarm);
                return;
            }
            OffAlarm(allowNotifyAlarm);
        }
    }

    /// <summary>
    /// Alarm tag for continuous/timer registers that count up (1, 2, 3...).
    /// - OnAlarm (OccurrenceTime): when value transitions from 0 to > 0.
    /// - Ignore: when value continues changing but stays > 0 (no log spam).
    /// - OffAlarm (RestoreTime): when value transitions from > 0 back to 0.
    /// 
    /// Subscribes additional event handlers that bypass the base class 5s debounce.
    /// Timer registers change value every second, so the base CheckAlarm's
    /// "wait 5s then check if value stabilized" logic causes isBusy deadlock.
    /// Our handlers evaluate immediately; OnAlarm/OffAlarm status guards
    /// prevent duplicate notifications.
    /// </summary>
    public class ContinuousAlarmTag : AlarmTagBase
    {
        public ContinuousAlarmTag(iDriver driver, AlarmParam param) : base(driver, param)
        {
            // Base constructor already subscribed TagValueChanged/TagStatusChanged
            // which route through CheckAlarm (5s debounce). Those will deadlock for
            // continuous registers (harmless but inert). We add our own handlers
            // that fire alongside them via multicast delegate, evaluating directly.
            if (this.tag != null)
            {
                this.tag.TagValueChanged += ContinuousTagValueChanged;
            }
        }

        private void ContinuousTagValueChanged(object sender, TagValueEventArgs e)
        {
            if (this.tag.Status != "Good") return;
            var value = this.tag.Value;
            if (string.IsNullOrEmpty(value)) return;

            CheckCondition(value, true);
        }

        protected override void CheckCondition(string value, bool allowNotifyAlarm)
        {
            if (this.tag.Status != "Good") return;

            double numericValue;
            if (!double.TryParse(value, out numericValue)) return;

            if (numericValue > 0)
            {
                // Value is > 0: trigger alarm only on rising edge (0 -> > 0)
                // OnAlarm internally guards with: if (Status == ALARM) return;
                // So subsequent calls while still > 0 are no-ops.
                OnAlarm(allowNotifyAlarm);
            }
            else
            {
                // Value is 0: restore alarm (> 0 -> 0)
                // OffAlarm internally guards with: if (Status == NORMAL) return;
                OffAlarm(allowNotifyAlarm);
            }
        }
    }
}
