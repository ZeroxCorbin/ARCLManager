using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
        public delegate void JobCompleteEventHandler(object sender, QueueJobUpdateEventArgs data);
        public event JobCompleteEventHandler JobComplete;



        public Dictionary<string, QueueManagerJob> _Jobs { get; private set; }
        public ReadOnlyDictionary<string, QueueManagerJob> Jobs
        {
            get
            {
                lock (JobsLock)
                    return new ReadOnlyDictionary<string, QueueManagerJob>(_Jobs);
            }
        }
        private object JobsLock { get; set; } = new object();

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
                Connection.ReceiveAsync();

            Connection.QueueJobUpdate += Connection_QueueJobUpdate;

            _Jobs = new Dictionary<string, QueueManagerJob>();

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

        public bool QueueMulti(List<QueueJobUpdateEventArgs> goals)
        {
            StringBuilder msg = new StringBuilder();
            string space = " ";

            msg.Append("QueueMulti");
            msg.Append(space);

            msg.Append(goals.Count.ToString());
            msg.Append(space);

            msg.Append("2");
            msg.Append(space);

            foreach (QueueJobUpdateEventArgs g in goals)
            {
                msg.Append(g.GoalName);
                msg.Append(space);

                msg.Append(Enum.GetName(typeof(QueueJobUpdateEventArgs.GoalTypes), g.GoalType));
                msg.Append(space);

                msg.Append(g.Priority.ToString());
                msg.Append(space);
            }

            if (!string.IsNullOrEmpty(goals[0].JobID))
                msg.Append(goals[0].JobID);

            return Connection.Write(msg.ToString());
        }
        public bool UpdateJobSegment(ref QueueManagerJob job, string newGoalName, int segmentIndex = 1)
        {
            switch (job.Goals[segmentIndex].Status)
            {
                case ARCLStatus.Pending:
                    break;
                case ARCLStatus.InProgress:
                    switch (job.Goals[segmentIndex].SubStatus)
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

            QueueModify(job.Goals[segmentIndex].ID, "goal", newGoalName);

            return true;
        }
        public bool CancelJob(ref QueueManagerJob job)
        {
            return true;
        }

        //Private
        private bool QueueShow() => Connection.Write("QueueShow");
        private bool QueueShow(ARCLJobStatusRequestTypes status) => Connection.Write($"QueueShow status {status}");
        private bool QueueModify(string id, string type, string value) => Connection.Write($"queueModify {id} {type} {value}");

        private void Connection_QueueJobUpdate(object sender, QueueJobUpdateEventArgs data)
        {
            lock (JobsLock)
            {
                if (data.IsEnd & !IsSynced)
                {
                    IsSynced = true;
                    Connection.Queue(false, new Action(() => InSync?.Invoke(this, true)));
                    return;
                }

                if (!_Jobs.ContainsKey(data.JobID))
                {
                    QueueManagerJob job = new QueueManagerJob(data);
                    _Jobs.Add(job.ID, job);
                }
                else
                {
                    int i = 0;
                    bool found = false;
                    foreach (QueueJobUpdateEventArgs currentQue in _Jobs[data.JobID].Goals.ToList())
                    {
                        if (currentQue.ID.Equals(data.ID))
                        {
                            _Jobs[data.JobID].Goals[i] = data;
                            found = true;
                        }

                        i++;
                    }
                    if (!found) _Jobs[data.JobID].AddGoal(data);
                }

                if (_Jobs[data.JobID].Status == ARCLStatus.Completed || _Jobs[data.JobID].Status == ARCLStatus.Cancelled)
                    Connection.Queue(false, new Action(() => JobComplete?.Invoke(new object(), data)));
            }

        }
    }
}
