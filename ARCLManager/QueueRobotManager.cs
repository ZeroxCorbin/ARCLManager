using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class QueueRobotManager
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
        public QueueRobotManager(ARCLConnection connection) => Connection = connection;

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
            Robots.Clear();

            ThreadPool.QueueUserWorkItem(new WaitCallback(QueueShowRobot_Thread));

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "QueueShowRobot";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        private void QueueShowRobot_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.QueueRobotUpdate += Connection_QueueRobotUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.TRUE)
                        Stopwatch.Reset();

                    Connection.Write("queueShowRobot");

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State == SyncStates.DELAYED)
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
                Connection.QueueRobotUpdate -= Connection_QueueRobotUpdate;
            }
        }
        private void Connection_QueueRobotUpdate(object sender, QueueRobotUpdateEventArgs data)
        {
            Heartbeat = true;
            TTL = Stopwatch.ElapsedMilliseconds;

            if(data.IsEnd)
            {
                if(SyncState.State != SyncStates.TRUE)
                {
                    SyncState.State = SyncStates.TRUE;
                    SyncState.Message = "EndQueueShowRobot";
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }
                return;
            }

            if(!Robots.ContainsKey(data.Name))
                while(!Robots.TryAdd(data.Name, data)) { Robots.Locked = false; }
            else
                Robots[data.Name] = data;
        }


        public ReadOnlyConcurrentDictionary<string, QueueRobotUpdateEventArgs> Robots { get; set; } = new ReadOnlyConcurrentDictionary<string, QueueRobotUpdateEventArgs>(10, 100);
        public bool IsRobotAvailable => RobotsAvailable > 0;
        public int RobotsAvailable
        {
            get
            {
                if(SyncState.State!=SyncStates.TRUE)
                    return 0;

                int cnt = 0;

                foreach(KeyValuePair<string, QueueRobotUpdateEventArgs> robot in Robots)
                    if(robot.Value.Status == ARCLStatus.Available)
                        cnt++;
                return cnt;
            }
        }
        public int RobotsUnAvailable => Robots.Count - RobotsAvailable;
    }
}
