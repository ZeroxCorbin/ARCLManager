using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
    public class QueueJobManager
    {
        public delegate void SyncStateChangeEventHandler(object sender, SyncStateEventArgs syncState);
        public event SyncStateChangeEventHandler SyncStateChange;
        public SyncStateEventArgs SyncState { get; private set; } = new SyncStateEventArgs();

        private ARCLConnection Connection { get; set; }
        public QueueJobManager() { }
        public QueueJobManager(ARCLConnection connection) => Connection = connection;

        public bool Start()
        {
            if(Connection == null || !Connection.IsConnected)
                return false;
            if(!Connection.StartReceiveAsync())
                return false;

            Start_();

            return true;
        }
        public bool Start(ARCLConnection connection)
        {
            Connection = connection;
            return Start();
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
        public bool WaitForSync(long timeout = 30000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(SyncState.State != SyncStates.TRUE & sw.ElapsedMilliseconds < timeout) { Thread.Sleep(1); }

            return SyncState.State == SyncStates.TRUE;
        }

        private void Start_()
        {
            Connection.QueueJobUpdate += Connection_QueueJobUpdate;

            Jobs.Clear();

            Connection.Write("QueueShow");

            SyncState.State = SyncStates.FALSE;
            SyncState.Message = "QueueShow";
            Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
        }
        private void Stop_()
        {
            if(Connection != null)
                Connection.QueueJobUpdate -= Connection_QueueJobUpdate;
        }

        private void Connection_QueueJobUpdate(object sender, QueueManagerJobSegment data)
        {
            if(data.IsEnd)
            {
                if(SyncState.State != SyncStates.TRUE)
                {
                    SyncState.State = SyncStates.TRUE;
                    SyncState.Message = "EndQueueShow";
                    Connection.QueueTask(true, new Action(() => SyncStateChange?.Invoke(this, SyncState)));
                }
                return;
            }

            if(!Jobs.ContainsKey(data.JobID))
            {

                QueueManagerJob job = new QueueManagerJob(data);
                while(!Jobs.TryAdd(job.ID, job)) { Jobs.Locked = false; }

            }
            else
            {
                int i = 0;
                bool found = false;
                foreach(KeyValuePair<string, QueueManagerJobSegment> seg in Jobs[data.JobID].Segments)
                {
                    if(seg.Value.ID.Equals(data.ID))
                    {
                        Thread.Sleep(1);
                        Jobs[data.JobID].Segments[seg.Key] = data;
                        found = true;
                        break;
                    }
                    i++;
                }

                if(!found)
                    Jobs[data.JobID].AddSegment(data);
            }

            if(Jobs[data.JobID].Status == ARCLStatus.Completed || Jobs[data.JobID].Status == ARCLStatus.Cancelled)
            {
                while(!Jobs.TryRemove(data.JobID, out QueueManagerJob job)) { Jobs.Locked = false; }

                Connection.QueueTask(false, new Action(() => JobComplete?.Invoke(new object(), data)));
            }

        }


        public delegate void JobCompleteEventHandler(object sender, QueueManagerJobSegment data);
        public event JobCompleteEventHandler JobComplete;

        public ReadOnlyConcurrentDictionary<string, QueueManagerJob> Jobs { get; private set; } = new ReadOnlyConcurrentDictionary<string, QueueManagerJob>(10, 100);

        public bool QueueMulti(List<QueueManagerJobSegment> segments)
        {
            StringBuilder msg = new StringBuilder();
            string space = " ";

            msg.Append("QueueMulti");
            msg.Append(space);

            msg.Append(segments.Count.ToString());
            msg.Append(space);

            msg.Append("2");
            msg.Append(space);

            foreach(QueueManagerJobSegment g in segments)
            {
                msg.Append(g.GoalName);
                msg.Append(space);

                msg.Append(Enum.GetName(typeof(QueueManagerJobSegment.Types), g.Type));
                msg.Append(space);

                msg.Append(g.Priority.ToString());
                msg.Append(space);
            }

            if(!string.IsNullOrEmpty(segments[0].JobID))
                msg.Append(segments[0].JobID);

            return Connection.Write(msg.ToString());
        }
        /// <summary>
        /// Modify a the goal of a job segment.
        /// </summary>
        /// <param name="jobID">The ID of the job to be updated.</param>
        /// <param name="newGoalName">The new goal name for the segment being modified.</param>
        /// <param name="segmentID">The ID of the segment to be mdified with the new goal name.</param>
        /// <param name="timeout">Wait (timeout) ms for the segment to change.</param>
        /// <returns>Returns true if the segment is modified.
        /// Returns false if it takes longer than (timeout) ms to modify the segment.</returns>
        public bool ModifySegment(string jobID, string newGoalName, string segmentID, int timeout = 10000)
        {
            if(!Jobs.ContainsKey(jobID))
                return false;

            if(!Jobs[jobID].Segments.ContainsKey(segmentID))
                return false;

            switch(Jobs[jobID].Segments[segmentID].Status)
            {
                case ARCLStatus.Pending:
                    break;
                case ARCLStatus.InProgress:
                    switch(Jobs[jobID].Segments[segmentID].SubStatus)
                    {
                        case ARCLSubStatus.UnAllocated:
                        case ARCLSubStatus.Allocated:
                        case ARCLSubStatus.Driving:
                            break;
                        default:
                            return false;
                    }
                    break;
                default:
                    return false;
            }

            QueueModify(Jobs[jobID].Segments[segmentID].ID, "goal", newGoalName);

            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(Jobs[jobID].Segments[segmentID].GoalName != newGoalName)
            {
                if(sw.ElapsedMilliseconds > timeout)
                    break;
            }

            if(Jobs[jobID].Segments[segmentID].GoalName.Equals(newGoalName))
                return true;
            else
                return false;
        }
        /// <summary>
        /// Cancel a job in the queue.
        /// </summary>
        /// <param name="jobID">The ID of the job to be cancelled.</param>
        /// <returns>Returns true if the job is removed from the Jobs dictionary or the jobID is not in the dictionary.
        /// Returns false if it takes longer than (timeout) ms to remove the job from the Jobs dictionary.</returns>
        public bool CancelJob(string jobID, int timeout = 10000)
        {
            if(Jobs.ContainsKey(jobID))
                QueueCancel("jobid", jobID);
            else
                return true;

            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while(Jobs.ContainsKey(jobID))
            {
                if(sw.ElapsedMilliseconds > timeout)
                    break;
            }

            if(Jobs.ContainsKey(jobID))
                return false;
            else
                return true;
        }

        //Private
        private bool QueueShow(ARCLJobStatusRequestTypes status) => Connection.Write($"queueShow status {status}");
        private bool QueueModify(string id, string type, string value) => Connection.Write($"queueModify {id} {type} {value}");
        private bool QueueCancel(string type, string value) => Connection.Write($"queueCancel {type} {value}");



    }
}
