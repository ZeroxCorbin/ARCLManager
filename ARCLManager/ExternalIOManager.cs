using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class ExternalIOManager
    {
        /// <summary>
        /// The Delegate for the SyncStateChange Event.
        /// </summary>
        /// <param name="sender">A reference to this class.</param>
        /// <param name="syncState">The state of the dictionary Keys. See the SyncState property for details.</param>
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        /// <summary>
        /// Raised when SyncState property changes.
        /// See the SyncState property for details.
        /// </summary>
        public event SyncStateChangeEventHandler SyncStateChange;
        /// <summary>
        /// The state of the Managers dictionary.
        /// State= WAIT; Wait to access the dictionary.
        ///              Calling Start() or Stop() sets this state.
        /// State= DELAYED; The dictionary Values are not valid.
        ///                 The dictionary Values being updated from the ARCL Server are delayed.
        /// State= UPDATING; The dictionary Values are being updated.
        /// State= OK; The dictionary is up to date.
        /// </summary>
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();
        public bool IsSynced => SyncState.State == SyncStates.OK;
        /// <summary>
        /// A reference to the connection to the ARCL Server.
        /// </summary>
        private ARCLConnection Connection { get; set; }
        /// <summary>
        /// Start the manager.
        /// This will load the dictionary.
        /// </summary>
        /// <param name="updateRate">How often to send a request to update the dictionary's Values.</param>
        /// <returns>False: Connection issue.</returns>
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
        /// <summary>
        /// Start the manager.
        /// This will load the dictionary.
        /// </summary>
        /// <param name="updateRate">How often to send a request to update the dictionary's Values.</param>
        /// <param name="connection">A connected ARCLConnection.</param>
        /// <returns>False: Connection issue.</returns>
        public bool Start(int updateRate, ARCLConnection connection)
        {
            UpdateRate = updateRate;
            Connection = connection;

            return Start(updateRate);
        }
        /// <summary>
        /// Stop the manager.
        /// </summary>
        public void Stop()
        {
            if(SyncState.State != SyncStates.WAIT)
            {
                SyncState.State = SyncStates.WAIT;
                SyncState.Message = "Stop";
                Connection?.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
            }
            Connection?.StopReceiveAsync();

            Stop_();
        }
        /// <summary>
        /// Wait for the dictionary to be in sync with the ARCL server data.
        /// After calling Start(), you can either call this method or wait for the SyncStateChanged event. 
        /// </summary>
        /// <param name="timeout">Wait for SyncState.State.OK for milliseconds.</param>
        /// <returns>False: Timeout waiting for SyncState.State.OK.</returns>
        public bool WaitForSync(long timeout = 30000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(SyncState.State != SyncStates.OK & sw.ElapsedMilliseconds < timeout) { Thread.Sleep(1); }

            return SyncState.State == SyncStates.OK;
        }
        /// <summary>
        /// Is the update thread running?
        /// </summary>
        public bool IsRunning { get; private set; } = false;
        /// <summary>
        /// Time between updates of the dictionary Values.
        /// If this is consistently greater than the UpdateRate passed to Start(), consider lowering the update rate.
        /// </summary>
        public long TTL { get; private set; } = 0;
        /// <summary>
        /// Stores the provided update rate for the Update_Thread. (ms)
        /// </summary>
        private int UpdateRate { get; set; } = 500;
        /// <summary>
        /// Used to time the message response. (TTL)
        /// </summary>
        private Stopwatch Stopwatch { get; } = new Stopwatch();
        /// <summary>
        /// Used to track if a message response was received before sending the next request.
        /// </summary>
        private bool Heartbeat { get; set; } = false;

        private void Start_()
        {
            ActiveSets.Clear();
            InProcessSets.Clear();

            ThreadPool.QueueUserWorkItem(new WaitCallback(ExternalIOUpdate_Thread));

            SyncState.State = SyncStates.WAIT;
            SyncState.Message = "ExtIODump";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            IsRunning = false;
            Thread.Sleep(UpdateRate + 100);
        }

        public ExternalIOManager() { }
        public ExternalIOManager(ARCLConnection connection) => Connection = connection;

        private void ExternalIOUpdate_Thread(object sender)
        {
            IsRunning = true;
            Stopwatch.Reset();

            Connection.ExternalIOUpdate += Connection_ExternalIOUpdate;

            try
            {
                while(IsRunning)
                {
                    if(SyncState.State == SyncStates.OK)
                        Stopwatch.Reset();

                    if(Connection.IsConnected)
                        Connection.Write("extIODump");
                    else
                    {
                        Stop();
                        return;
                    }

                    Heartbeat = false;

                    Thread.Sleep(UpdateRate);

                    if(Heartbeat)
                    {
                        if(SyncState.State == SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.OK;
                            SyncState.Message = "ExtIODump";
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                    else
                    {
                        if(SyncState.State != SyncStates.DELAYED)
                        {
                            SyncState.State = SyncStates.DELAYED;
                            SyncState.Message = "ExtIODump";
                            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                        }
                    }
                }
            }
            finally
            {
                IsRunning = false;
                Connection.ExternalIOUpdate -= Connection_ExternalIOUpdate;
            }
        }
        private void Connection_ExternalIOUpdate(object sender, ExternalIOUpdateEventArgs data)
        {
            if(data.ExtIOSet == null) return;

            if(data.ExtIOSet.IsEnd)
            {
                Heartbeat = true;
                TTL = Stopwatch.ElapsedMilliseconds;

                if(SyncDesiredSets())
                {
                    if(SyncState.State != SyncStates.OK)
                    {
                        SyncState.State = SyncStates.OK;
                        SyncState.Message = "EndExtIODump";
                        Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                    }
                }
                return;
            }

            if(data.ExtIOSet.IsDump)
            {
                IsIOUpdate = false;

                if(ActiveSets.ContainsKey(data.ExtIOSet.Name))
                    ActiveSets[data.ExtIOSet.Name] = data.ExtIOSet;
                else
                {
                    while(!ActiveSets.TryAdd(data.ExtIOSet.Name, data.ExtIOSet)) { ActiveSets.Locked = false; }

                    IsIOUpdate = true;
                }

                foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                    IsIOUpdate |= set.Value.AddedForPendingUpdate;

                if(SyncState.State != SyncStates.OK)
                    if(!IsIOUpdate)
                    {
                        SyncState.State = SyncStates.OK;
                        Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                    }

                return;
            }

            if(data.ExtIOSet.HasInputs)
            {
                IsIOUpdate = false;

                if(ActiveSets.ContainsKey(data.ExtIOSet.Name))
                {
                    ActiveSets[data.ExtIOSet.Name].Inputs = data.ExtIOSet.Inputs;
                    ActiveSets[data.ExtIOSet.Name].AddedForPendingUpdate = false;
                }
                else
                    IsIOUpdate = true;

                foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                    IsIOUpdate |= set.Value.AddedForPendingUpdate;

                if(!IsIOUpdate)
                {
                    SyncState.State = SyncStates.OK;
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }

                return;
            }
        }

        public ReadOnlyConcurrentDictionary<string, ExtIOSet> ActiveSets { get; } = new ReadOnlyConcurrentDictionary<string, ExtIOSet>(10, 100);
        public ReadOnlyDictionary<string, ExtIOSet> DesiredSets { get; private set; }

        private Dictionary<string, ExtIOSet> InProcessSets { get; set; } = new Dictionary<string, ExtIOSet>();
        private bool IsIOUpdate { get; set; } = false;

        public bool WriteAllInputs()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(SyncState.State != SyncStates.OK)
                return false;
            if(ActiveSets.Count == 0)
                return false;

            bool res = false;
            for(int i = 1; i <= ActiveSets.Count; i++)
            {
                ActiveSets[i.ToString()].AddedForPendingUpdate = true;
                res &= Connection.Write(ActiveSets[i.ToString()].WriteInputCommand);
            }

            return res ^= true;
        }
        public bool ReadAllOutputs()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(SyncState.State != SyncStates.OK)
                return false;
            if(ActiveSets.Count == 0)
                return false;

            return Dump();
        }

        private bool Dump()
        {
            foreach(KeyValuePair<string, ExtIOSet> set in ActiveSets)
                set.Value.AddedForPendingUpdate = true;

            return Connection.Write("extIODump");
        }
        private bool SyncDesiredSets()
        {
            if(DesiredSets.Count == 0)
                return true;

            foreach(KeyValuePair<string, ExtIOSet> set in DesiredSets)
            {
                if(ActiveSets.ContainsKey(set.Key))
                {
                    if(InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Remove(set.Key);
                    continue;
                }
                else
                {
                    if(!InProcessSets.ContainsKey(set.Key))
                        InProcessSets.Add(set.Key, set.Value);
                }
            }

            if(InProcessSets.Count() > 0)
                AddSets();
            else
                return true;

            return false;
        }
        private void AddSets()
        {
            foreach(KeyValuePair<string, ExtIOSet> set in InProcessSets)
            {
                if(set.Value.AddedForPendingUpdate) continue;
                else
                    Connection.Write(set.Value.CreateSetCommand);
            }

            ThreadPool.QueueUserWorkItem(new WaitCallback(CreateIOWaitThread));
        }
        private void CreateIOWaitThread(object sender)
        {
            Thread.Sleep(10000);

            Dump();
        }


    }
}
