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
        public List<string> DeviceNames
        {
            get
            {
                string[] strs = new string[Devices.Keys.Count];
                Devices.Keys.CopyTo(strs, 0);
                return new List<string>(strs);
            }
        }

        public bool Start()
        {
            if (!Connection.IsConnected)
                return false;

            Connection.RangeDevice += Connection_RangeDevice;

            return RangeDeviceList();
        }

        public void Stop()
        {
            if (IsSynced)
            {
                IsSynced = false;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, IsSynced)));
            }

            Connection.RangeDeviceCurrentUpdate -= Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate -= Connection_RangeDeviceCumulativeUpdate;
            Connection.RangeDevice -= Connection_RangeDevice;

            Connection?.StopReceiveAsync();

            Devices.Clear();
        }

        private void Connection_RangeDevice(object sender, RangeDeviceEventArgs device)
        {
            if (device.IsEnd)
            {
                Connection.RangeDevice -= Connection_RangeDevice;

                Connection.RangeDeviceCurrentUpdate += Connection_RangeDeviceCurrentUpdate;
                Connection.RangeDeviceCumulativeUpdate += Connection_RangeDeviceCumulativeUpdate;

                IsSynced = true;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, IsSynced)));
                return;
            }
            if (!string.IsNullOrEmpty(device.RangeDevice.Name))
                while (!Devices.TryAdd(device.RangeDevice.Name, device.RangeDevice)) { Devices.Locked = false; }
        }

        private bool RangeDeviceList() => Connection.Write("rangeDeviceList");


        private void Connection_RangeDeviceCurrentUpdate(object sender, ARCLTypes.RangeDeviceReadingUpdateEventArgs data)
        {
            if (Devices.ContainsKey(data.Name))
                Devices[data.Name].CumulativeReadings = data;
        }

        private void Connection_RangeDeviceCumulativeUpdate(object sender, ARCLTypes.RangeDeviceReadingUpdateEventArgs data)
        {
            if (Devices.ContainsKey(data.Name))
                Devices[data.Name].CurrentReadings = data;
        }
    }
}
