using ARCLTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ARCL
{
    public class RangeDeviceManager
    {
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        public event SyncStateChangeEventHandler SyncStateChange;
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();

        public bool IsRunning { get; private set; } = false;
        public long TTL { get; private set; } = 0;

        private int UpdateRate { get; set; } = 500;
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        private bool Heartbeat { get; set; } = false;

        private ARCLConnection Connection { get; set; }
        public RangeDeviceManager(ARCLConnection connection) => Connection = connection;

        public bool Start(int updateRate)
        {
            UpdateRate = updateRate;

            if(Connection == null || !Connection.IsConnected)
                return false;
            if(!Connection.StartReceiveAsync())
                return false;

            Start_();

            return true;
        }
        public bool Start(int updateRate, ARCLConnection connection)
        {
            UpdateRate = updateRate;
            Connection = connection;

            return Start(updateRate);
        }
        public void Stop()
        {
            if(SyncState.State != SyncStates.FALSE)
            {
                SyncState.State = SyncStates.FALSE;
                SyncState.Message = "Stop";
                Connection?.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
            }
            Connection?.StopReceiveAsync();

            Stop_();
        }

        private void Start_()
        {
            Connection.RangeDevice += Connection_RangeDevice;

            Devices.Clear();

            Connection.Write("rangeDeviceList");

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "RangeDeviceList";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            if(Connection != null)
                Connection.RangeDevice -= Connection_RangeDevice;

            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        private void RangeDeviceUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.RangeDeviceCurrentUpdate += Connection_RangeDeviceCurrentUpdate;
            Connection.RangeDeviceCumulativeUpdate += Connection_RangeDeviceCumulativeUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.TRUE)
                        Stopwatch.Reset();

                    foreach(KeyValuePair<string, RangeDevice> l in Devices)
                        Connection.Write("rangeDeviceGetCurrent " + l.Key);

                    foreach(KeyValuePair<string, RangeDevice> l in Devices)
                        Connection.Write("rangeDeviceGetCumulative " + l.Key);

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State != SyncStates.TRUE)
                        {
                            SyncState.State = SyncStates.TRUE;
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                    else
                    {
                        if(SyncState.State != SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.DELAYED;
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
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
        private void Connection_RangeDevice(object sender, RangeDeviceEventArgs device)
        {
            if(device.IsEnd)
            {
                Connection.RangeDevice -= Connection_RangeDevice;
                ThreadPool.QueueUserWorkItem(new WaitCallback(RangeDeviceUpdate_Thread));

                if(SyncState.State != SyncStates.TRUE)
                {
                    SyncState.State = SyncStates.TRUE;
                    SyncState.Message = "EndRangeDeviceList";
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }
                return;
            }
            if(!string.IsNullOrEmpty(device.RangeDevice.Name))
                while(!Devices.TryAdd(device.RangeDevice.Name, device.RangeDevice)) { Devices.Locked = false; }
        }


        public ReadOnlyConcurrentDictionary<string, RangeDevice> Devices { get; private set; } = new ReadOnlyConcurrentDictionary<string, RangeDevice>(10, 100);

        public delegate void RangeDeviceCurrentReceivedEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCurrentReceivedEventHandler RangeDeviceCurrentUpdate;

        public delegate void RangeDeviceCumulativeUpdateEventHandler(RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCumulativeUpdateEventHandler RangeDeviceCumulativeUpdate;

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



    }
}
