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
        /// <summary>
        /// Raised when the Jobs list is sycronized with the EM job queue.
        /// </summary>
        public delegate void InSyncEventHandler(object sender, bool state);
        public event InSyncEventHandler InSync;
        /// <summary>
        /// True when the Jobs list is sycronized with the EM job queue.
        /// </summary>
        public bool IsSynced { get; private set; } = false;

        private ARCLConnection Connection { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        public delegate void JobCompleteEventHandler(object sender, QueueManagerJobSegment data);
        public event JobCompleteEventHandler JobComplete;

        public ReadOnlyConcurrentDictionary<string, QueueManagerJob> Jobs { get; private set; } = new ReadOnlyConcurrentDictionary<string, QueueManagerJob>(10, 100);
        private object JobUpdateLock { get; set; } = new object();

        //Public
        public QueueJobManager(ARCLConnection connection) => Connection = connection;

        /// <summary>
        /// Starts the QueueManager.
        /// This will clear and reload the Jobs list.
        /// Stop() must be called before Nulling QM or callling Start() again.
        /// </summary>
        public void Start()
        {
            if (!Connection.IsReceivingAsync)
                Connection.StartReceiveAsync();

            Connection.QueueJobUpdate += Connection_QueueJobUpdate;

            //_Jobs = new Dictionary<string, QueueManagerJob>();

            //Initiate the the load of the current queue
            QueueShow();
        }
        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            Connection.QueueJobUpdate -= Connection_QueueJobUpdate;
            Connection.StopReceiveAsync();
        }

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

            foreach (QueueManagerJobSegment g in segments)
            {
                msg.Append(g.GoalName);
                msg.Append(space);

                msg.Append(Enum.GetName(typeof(QueueManagerJobSegment.Types), g.Type));
                msg.Append(space);

                msg.Append(g.Priority.ToString());
                msg.Append(space);
            }

            if (!string.IsNullOrEmpty(segments[0].JobID))
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
            if (!Jobs.ContainsKey(jobID))
                return false;

            if (!Jobs[jobID].Segments.ContainsKey(segmentID))
                return false;

            switch (Jobs[jobID].Segments[segmentID].Status)
            {
                case ARCLStatus.Pending:
                    break;
                case ARCLStatus.InProgress:
                    switch (Jobs[jobID].Segments[segmentID].SubStatus)
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

            while (Jobs[jobID].Segments[segmentID].GoalName != newGoalName)
            {
                if (sw.ElapsedMilliseconds > timeout)
                    break;
            }

            if (Jobs[jobID].Segments[segmentID].GoalName.Equals(newGoalName))
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
            if (Jobs.ContainsKey(jobID))
                QueueCancel("jobid", jobID);
            else
                return true;

            Stopwatch sw = new Stopwatch();
            sw.Restart();

            while (Jobs.ContainsKey(jobID))
            {
                if (sw.ElapsedMilliseconds > timeout)
                    break;
            }

            if (Jobs.ContainsKey(jobID))
                return false;
            else
                return true;
        }
        //Private
        private bool QueueShow() => Connection.Write("QueueShow");
        private bool QueueShow(ARCLJobStatusRequestTypes status) => Connection.Write($"queueShow status {status}");
        private bool QueueModify(string id, string type, string value) => Connection.Write($"queueModify {id} {type} {value}");
        private bool QueueCancel(string type, string value) => Connection.Write($"queueCancel {type} {value}");


        private void Connection_QueueJobUpdate(object sender, QueueManagerJobSegment data)
        {
            if (data.IsEnd & !IsSynced)
            {
                IsSynced = true;
                Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, true)));
                return;
            }
            if (data.IsEnd) return;

            lock (JobUpdateLock)
            {
                if (!Jobs.ContainsKey(data.JobID))
                {

                    QueueManagerJob job = new QueueManagerJob(data);
                    while (!Jobs.TryAdd(job.ID, job)) { Jobs.Locked = false; }

                }
                else
                {
                    int i = 0;
                    bool found = false;
                    foreach (KeyValuePair<string, QueueManagerJobSegment> seg in Jobs[data.JobID].Segments)
                    {
                        if (seg.Value.ID.Equals(data.ID))
                        {
                            Thread.Sleep(1);
                            Jobs[data.JobID].Segments[seg.Key] = data;
                            found = true;
                            break;
                        }
                        i++;
                    }

                    if (!found)
                        Jobs[data.JobID].AddSegment(data);
                }

                if (Jobs[data.JobID].Status == ARCLStatus.Completed || Jobs[data.JobID].Status == ARCLStatus.Cancelled)
                {
                    while (!Jobs.TryRemove(data.JobID, out QueueManagerJob job)) { Jobs.Locked = false; }

                    Connection.QueueTask(false, new Action(() => JobComplete?.Invoke(new object(), data)));
                }
            }
        }
    }
}
