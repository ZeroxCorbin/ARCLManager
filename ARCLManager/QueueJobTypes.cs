using System;
using System.Collections.Generic;

namespace ARCLTypes
{
    public class QueueJobUpdateEventArgs : EventArgs
    {
        public enum GoalTypes
        {
            pickup,
            dropoff
        }

        public string Message { get; }
        public string ID { get; }
        public GoalTypes GoalType { get; }
        public int Order { get; }
        public string JobID { get; }
        public int Priority { get; }
        public ARCLStatus Status { get; }
        public ARCLSubStatus SubStatus { get; }
        public string GoalName { get; }
        public string RobotName { get; }
        public DateTime StartedOn { get; }
        public DateTime CompletedOn { get; }
        public int FailCount { get; }
        public bool IsEnd { get; }

        public QueueJobUpdateEventArgs(string goalName, GoalTypes goalType, int priority = 10)
        {
            GoalName = goalName;
            Priority = priority;
            GoalType = goalType;

            Status = ARCLStatus.Pending;
            SubStatus = ARCLSubStatus.None;
        }

        public QueueJobUpdateEventArgs(string msg)
        {
            Message = msg;

            string[] spl = msg.Split(' ');

            if (spl[0].StartsWith("EndQueueShow", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }

            //QueueMulti: goal "Goal1" with priority 10 id PICKUP12 and job_id OWBQYSXSGZ successfully queued
            if (spl[0].StartsWith("queuemulti", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (spl.Length < 13)
                        throw new Exception();

                    GoalName = spl[2].Replace("\"", "");

                    if (int.TryParse(spl[5], out int pri))
                        Priority = pri;
                    else
                        throw new Exception();

                    ID = spl[7];

                    if (ID.StartsWith("PICKUP"))
                    {
                        GoalType = GoalTypes.pickup;
                        if (int.TryParse(ID.Replace("PICKUP", ""), out int val))
                            Order = val;
                        else
                            throw new Exception();

                    }
                    else if (ID.StartsWith("DROPOFF"))
                    {
                        GoalType = GoalTypes.dropoff;
                        if (int.TryParse(ID.Replace("DROPOFF", ""), out int val))
                            Order = val;
                        else
                            throw new Exception();
                    }
                    else
                    {
                        throw new Exception();
                    }

                    JobID = spl[10];

                }
                catch (Exception)
                {

                }

                return;
            }

            //QueueShow: <id> <jobId> <priority> <status> <substatus> Goal <"goalName"> <”robotName”>
            //           <queued date> <queued time> <completed date> <completed time> <echoString> <failed count>
            //QueueShow: PICKUP3 JOB3 10 Completed None Goal "1" "21" 11/14/2012 11:49:23 11/14/2012 11:49:23 "" 0
            if (spl[0].StartsWith("QueueShow", StringComparison.CurrentCultureIgnoreCase) || spl[0].StartsWith("QueueUpdate", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (spl.Length != 15)
                        throw new Exception();

                    ID = spl[1];

                    if (ID.StartsWith("PICKUP"))
                    {
                        GoalType = GoalTypes.pickup;
                        if (int.TryParse(ID.Replace("PICKUP", ""), out int val))
                            Order = val;
                        else
                            throw new Exception();

                    }
                    else if (ID.StartsWith("DROPOFF"))
                    {
                        GoalType = GoalTypes.dropoff;
                        if (int.TryParse(ID.Replace("DROPOFF", ""), out int val))
                            Order = val;
                        else
                            throw new Exception();
                    }
                    else
                    {
                        throw new Exception();
                    }

                    JobID = spl[2];

                    if (int.TryParse(spl[3], out int pri))
                        Priority = pri;
                    else
                        throw new Exception();

                    if (Enum.TryParse(spl[4], out ARCLStatus status))
                        Status = status;
                    else
                        throw new Exception();

                    if (!spl[5].StartsWith("ID_"))
                    {
                        if (Enum.TryParse(spl[5], out ARCLSubStatus subStatus))
                            SubStatus = subStatus;
                        else
                            throw new Exception();
                    }

                    GoalName = spl[7].Replace("\"", "");

                    RobotName = spl[8].Replace("\"", "");

                    if (!spl[9].Equals("None"))
                    {
                        if (DateTime.TryParse(spl[9] + " " + spl[10], out DateTime dt))
                            StartedOn = dt;
                    }
                    else
                        throw new Exception();

                    if (!spl[11].Equals("None"))
                    {
                        if (DateTime.TryParse(spl[11] + " " + spl[12], out DateTime dt))
                            CompletedOn = dt;
                    }

                    int i = 14;
                    if (spl[0].StartsWith("QueueUpdate"))
                        i = 13;

                    if (int.TryParse(spl[i], out int fail))
                        FailCount = fail;
                    else
                        throw new Exception();

                }
                catch (Exception)
                {

                }
                return;
            }
        }

    }
    public class QueueManagerJob
    {
        public string ID { get; }
        public int Priority
        {
            get
            {
                if (Goals.Count > 0)
                    return Goals[0].Priority;
                else
                    return 0;
            }
        }

        public int GoalCount => Goals.Count;

        public QueueJobUpdateEventArgs CurrentGoal
        {
            get
            {
                if (Goals.Count > 0)
                {
                    foreach (QueueJobUpdateEventArgs que in Goals)
                    {
                        if (que.Status != ARCLStatus.Completed)
                            return que;
                    }
                    return Goals[Goals.Count - 1];
                }
                else
                    return null;
            }
        }
        public List<QueueJobUpdateEventArgs> Goals { get; private set; } = new List<QueueJobUpdateEventArgs>();

        public ARCLStatus Status
        {
            get
            {
                if (Goals.Count > 0)
                {
                    foreach (QueueJobUpdateEventArgs que in Goals)
                    {
                        if (que.Status != ARCLStatus.Completed)
                            return que.Status;
                    }
                    return ARCLStatus.Completed;
                }
                else
                    return ARCLStatus.Loading;
            }
        }
        public ARCLSubStatus SubStatus
        {
            get
            {
                if (Goals.Count > 0)
                {
                    foreach (QueueJobUpdateEventArgs que in Goals)
                    {
                        if (que.Status != ARCLStatus.Completed)
                            return que.SubStatus;
                    }
                    return ARCLSubStatus.None;
                }
                else
                    return ARCLSubStatus.None;
            }
        }
        public DateTime StartedOn
        {
            get
            {
                if (Goals.Count > 0)
                    return Goals[0].StartedOn;
                else
                    return new DateTime();
            }
        }
        public DateTime CompletedOn
        {
            get
            {
                if (Goals.Count > 0)
                    return Goals[Goals.Count - 1].CompletedOn;
                else
                    return new DateTime();
            }
        }

        public QueueManagerJob(string id) => ID = id;

        public QueueManagerJob(QueueJobUpdateEventArgs goal)
        {
            ID = goal.JobID;
            AddQueAndSort(goal);
        }

        public void AddGoal(QueueJobUpdateEventArgs goal) => AddQueAndSort(goal);
        private void AddQueAndSort(QueueJobUpdateEventArgs goal)
        {
            Goals.Add(goal);
            Goals.Sort((foo1, foo2) => foo2.Order.CompareTo(foo1.Order));
        }
    }
    public class QueueManagerJobCompleteEventArgs : EventArgs
    {
        public QueueManagerJob Job { get; }
        public QueueManagerJobCompleteEventArgs(QueueManagerJob job)
        {
            Job = job;
        }
    }
}
