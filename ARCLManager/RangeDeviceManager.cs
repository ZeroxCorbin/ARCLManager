using ARCLTypes;
using System;
using System.Collections.Generic;

namespace ARCL
{
    public class RangeDeviceManager
    {
        /// <summary>
        /// Raised when the External IO is sycronized with the EM.
        /// </summary>
        public delegate void InSyncEventHandler(object sender, bool state);
        public event InSyncEventHandler InSync;
        /// <summary>
        /// True when the External IO is sycronized with the EM.
        /// </summary>
        public bool IsSynced { get; private set; } = false;

        private ARCLConnection Connection { get; set; }
        public StatusManager Status { get; private set; }
        public RangeDeviceManager(ARCLConnection connection, StatusManager status)
        {
            Connection = connection;
            Status = status;
        }
        
        public ReadOnlyConcurrentDictionary<string, RangeDevice> Devices { get; private set; } = new ReadOnlyConcurrentDictionary<string, RangeDevice>(10, 100);
        public List<string> DeviceNames { get; private set; }

        public bool Start()
        {
            using (SychronousCommands sync = new SychronousCommands(Connection.ConnectionString))
            {
                if (!sync.Connect())
                    return false;
                DeviceNames = sync.GetRangeDevices();
            }

            if (DeviceNames.Count == 0)
                return false;

            foreach (string str in DeviceNames)
                while (!Devices.TryAdd(str, new RangeDevice(str))) { Devices.Locked = false; }
            
            Connection.RangeDeviceCurrentUpdate += Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate += Connection_RangeDeviceCumulativeUpdate;

            return true;
        }

        public void Stop()
        {
            if (IsSynced)
            {
                IsSynced = false;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, false)));
            }

            Status.Stop();

            Connection.RangeDeviceCurrentUpdate -= Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate -= Connection_RangeDeviceCumulativeUpdate;

            Connection?.StopReceiveAsync();

            Devices.Clear();
        }

        private void Connection_RangeDeviceCurrentUpdate(object sender, ARCLTypes.RangeDeviceUpdateEventArgs data)
        {
            if (Devices.ContainsKey(data.Name))
                Devices[data.Name].CumulativeReadings = data;
        }

        private void Connection_RangeDeviceCumulativeUpdate(object sender, ARCLTypes.RangeDeviceUpdateEventArgs data)
        {
            if (Devices.ContainsKey(data.Name))
                Devices[data.Name].CurrentReadings = data;
        }
    }
}
