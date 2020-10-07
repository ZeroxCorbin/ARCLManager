using ARCLTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ARCL
{
    public class RangeDeviceManager
    {
        /// <summary>
        /// Raised when the Range Device List is syncronized.
        /// </summary>
        public delegate void IsSyncedEventHandler(bool state);
        public event IsSyncedEventHandler IsSyncedEvent;

        public delegate void IsDelayedEventHandler(bool state);
        public event IsDelayedEventHandler IsDelayedEvent;

        public delegate void RangeDeviceCurrentReceivedEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCurrentReceivedEventHandler RangeDeviceCurrentUpdate;

        public delegate void RangeDeviceCumulativeUpdateEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCumulativeUpdateEventHandler RangeDeviceCumulativeUpdate;


        /// <summary>
        /// True when the External IO is sycronized with the EM.
        /// </summary>
        public bool IsSynced { get; private set; } = false;
        public bool IsDelayed { get; private set; } = false;
        public bool IsRunning { get; private set; } = false;

        public long TTL { get; private set; }
        //Private
        private int UpdateRate { get; set; }
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        private bool Heartbeat { get; set; } = false;

        private ARCLConnection Connection { get; set; }
        public RangeDeviceManager(ARCLConnection connection) => Connection = connection;

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

        public bool Start(int updateRate)
        {
            UpdateRate = updateRate;

            if(!Connection.IsConnected)
                return false;

            Connection.RangeDevice += Connection_RangeDevice;

            return RangeDeviceList();
        }

        public void Stop()
        {
            if(IsSynced)
            {
                IsSynced = false;
                Connection.QueueTask(false, new Action(() => IsSyncedEvent?.Invoke(IsSynced)));
            }

            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);

            Connection?.StopReceiveAsync();

            Devices.Clear();
        }

        private void Connection_RangeDevice(object sender, RangeDeviceEventArgs device)
        {
            if(device.IsEnd)
            {
                Connection.RangeDevice -= Connection_RangeDevice;

                ThreadPool.QueueUserWorkItem(new WaitCallback(RangeDeviceUpdate_Thread));

                IsSynced = true;

                Connection.QueueTask(false, new Action(() => IsSyncedEvent?.Invoke(IsSynced)));
                return;
            }
            if(!string.IsNullOrEmpty(device.RangeDevice.Name))
                while(!Devices.TryAdd(device.RangeDevice.Name, device.RangeDevice)) { Devices.Locked = false; }
        }

        private bool RangeDeviceList() => Connection.Write("rangeDeviceList");

        private void Connection_RangeDeviceCurrentUpdate(object sender, RangeDeviceReadingUpdateEventArgs data)
        {
            if(Devices.ContainsKey(data.Name))
            {
                Devices[data.Name].CumulativeReadings = data;
                Devices[data.Name].CumulativeReadingsInSync = true;
            }

            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            Connection.QueueTask(true, new Action(() => RangeDeviceCurrentUpdate?.Invoke(data)));
        }

        private void Connection_RangeDeviceCumulativeUpdate(object sender, RangeDeviceReadingUpdateEventArgs data)
        {
            if(Devices.ContainsKey(data.Name))
            {
                Devices[data.Name].CurrentReadings = data;
                Devices[data.Name].CurrentReadingsInSync = true;
            }

            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            Connection.QueueTask(true, new Action(() => RangeDeviceCumulativeUpdate?.Invoke(data)));
        }


        private void RangeDeviceUpdate_Thread(object sender)
        {
            IsRunning = true;

            Connection.RangeDeviceCurrentUpdate += Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate += Connection_RangeDeviceCumulativeUpdate;

            try
            {
                while(IsRunning)
                {
                    if(!IsDelayed)
                        Stopwatch.Reset();

                    foreach(string l in DeviceNames)
                        Connection.Write("rangeDeviceGetCurrent " + l);

                    foreach(string l in DeviceNames)
                        Connection.Write("rangeDeviceGetCumulative " + l);

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(IsDelayed)
                        {
                            IsDelayed = false;
                            Connection.QueueTask(false, new Action(() => IsDelayedEvent?.Invoke(IsDelayed)));
                        }
                    }
                    else
                    {
                        if(!IsDelayed)
                        {
                            IsDelayed = true;
                            Connection.QueueTask(false, new Action(() => IsDelayedEvent?.Invoke(IsDelayed)));
                        }
                    }
                }
            }
            finally
            {
                IsRunning = false;
                Connection.RangeDeviceCurrentUpdate -= Connection_RangeDeviceCurrentUpdate;
                Connection.RangeDeviceCumulativeUpdate -= Connection_RangeDeviceCumulativeUpdate;
            }
        }
    }
}
