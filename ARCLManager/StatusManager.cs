using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class StatusManager
    {
        //Public
        public delegate void StatusUpdateEventHandler(object sender, StatusUpdateEventArgs data);
        public event StatusUpdateEventHandler StatusUpdate;

        public delegate void RangeDeviceCurrentReceivedEventHandler(object sender, RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCurrentReceivedEventHandler RangeDeviceCurrentUpdate;

        public delegate void RangeDeviceCumulativeUpdateEventHandler(object sender, RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCumulativeUpdateEventHandler RangeDeviceCumulativeUpdate;

        public delegate void StatusDelayedEventHandler(bool state);
        public event StatusDelayedEventHandler StatusDelayed;

        //Public Read-only
        public RangeDeviceManager RangeDeviceManager { get; private set; }
        public bool IsRunning { get; private set; } = false;
        public bool IsDelayed { get; private set; } = false;
        public long TTL { get; private set; }

        //Private
        private int UpdateRate { get; set; }
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        private bool Heartbeat { get; set; } = false;

        private ARCLConnection Connection { get; }
        public StatusManager(ARCLConnection connection) => Connection = connection;

        public void Start(int updateRate)
        {
            UpdateRate = updateRate;

            RangeDeviceManager = new RangeDeviceManager(Connection, this);
            RangeDeviceManager.Start();

            if (!Connection.IsReceivingAsync)
                Connection.ReceiveAsync();

            Connection.QueueTask(false, new Action(() => StatusUpdate_Thread()));
        }
        public void Stop()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);

            if (RangeDeviceManager == null)
                return;

            RangeDeviceManager.Stop();
            RangeDeviceManager = null;
        }

        //Private
        private void Connection_StatusUpdate(object sender, StatusUpdateEventArgs data)
        {
            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            Connection.QueueTask(false, new Action(() => StatusUpdate?.Invoke(sender, data)));
        }

        private void StatusUpdate_Thread()
        {
            IsRunning = true;
            Connection.StatusUpdate += Connection_StatusUpdate;

            try
            {
                while (IsRunning)
                {
                    if (!IsDelayed)
                        Stopwatch.Reset();

                    Connection.Write("onelinestatus");

                    if (RangeDeviceManager != null)
                    {
                        foreach (string l in RangeDeviceManager.DeviceNames)
                            Connection.Write("rangeDeviceGetCurrent " + l);

                        foreach (string l in RangeDeviceManager.DeviceNames)
                            Connection.Write("rangeDeviceGetCumulative " + l);
                    }

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if (Heartbeat)
                    {
                        if (IsDelayed)
                        {
                            IsDelayed = false;
                            Connection.QueueTask(false, new Action(() => StatusDelayed?.Invoke(false)));
                        }
                    }
                    else
                    {
                        if (!IsDelayed)
                        {
                            IsDelayed = true;
                            Connection.QueueTask(false, new Action(() => StatusDelayed?.Invoke(true)));
                        }
                    }
                }
            }
            finally
            {
                IsRunning = false;
                Connection.StatusUpdate -= Connection_StatusUpdate;
            }
        }
    }
}

