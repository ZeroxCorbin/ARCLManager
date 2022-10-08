using System;
using System.Collections.Generic;
using System.Linq;

namespace ARCLTypes
{
    public enum ARCLJobStatusRequestTypes
    {
        Pending,
        Interrupted,
        InProgress,
        Completed,
        Cancelled,
        Failed,
    }

    public class QueueManagerJobSegment : EventArgs
    {
        /// <summary>
        /// The orignal message to be parsed.
        /// </summary>
        public string Message { get; }
        /// <summary>
        /// Is there an Exception from parsing the Message?
        /// </summary>
        public bool IsMessageExeption { get; }
        /// <summary>
        /// The Exception from parsing the Message.
        /// Will be null if IsMessageException = false.
        /// </summary>
        public Exception Exception { get; } = null;
        /// <summary>
        /// Is this an EndOfCommand message?
        /// </summary>
        public bool IsEnd { get; } = false;

        public enum Types
        {
            pickup,
            dropoff
        }

        public string ID { get; }
        public Types Type { get; }
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


        public QueueManagerJobSegment(string jobID, string goalName, Types type, int priority = 10)
        {
            JobID = jobID;

            GoalName = goalName;
            Priority = priority;
            Type = type;

            Status = ARCLStatus.Pending;
            SubStatus = ARCLSubStatus.None;
        }

        public QueueManagerJobSegment(string msg)
        {
            Message = msg;

            string[] spl = msg.Split(' ');

            if (spl[0].StartsWith("EndQueueShow", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }
            //QueueMulti: goal "Goal1" with priority 10 id PICKUP12 and job_id OWBQYSXSGZ successfully queued
             else if (spl[0].StartsWith("QueueMulti", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (spl.Length < 13)
                        throw new Exception($"Invalid QueueMulti message length.");

                    GoalName = spl[2].Replace("\"", "");

                    if (int.TryParse(spl[5], out int pri))
                        Priority = pri;
                    else
                        throw new Exception($"Could not parse \"{spl[5]}\" to: Integer");

                    ID = spl[7];

                    if (ID.StartsWith("PICKUP"))
                    {
                        Type = Types.pickup;
                        if (int.TryParse(ID.Replace("PICKUP", ""), out int val))
                            Order = val;
                        else
                            throw new Exception($"Could not parse \"{ID.Replace("PICKUP", "")}\" to: Integer");

                    }
                    else if (ID.StartsWith("DROPOFF"))
                    {
                        Type = Types.dropoff;
                        if (int.TryParse(ID.Replace("DROPOFF", ""), out int val))
                            Order = val;
                        else
                            throw new Exception($"Could not parse \"{ID.Replace("DROPOFF", "")}\" to: Integer");
                    }
                    else
                    {
                        throw new Exception();
                    }

                    JobID = spl[10];

                }
                catch (Exception ex)
                {
                    Exception = ex;
                    IsMessageExeption = true;
                }

                return;
            }
            //QueueShow: <id> <jobId> <priority> <status> <substatus> Goal <"goalName"> <”robotName”>
            //           <queued date> <queued time> <completed date> <completed time> <echoString> <failed count>
            //QueueShow: PICKUP3 JOB3 10 Completed None Goal "1" "21" 11/14/2012 11:49:23 11/14/2012 11:49:23 "" 0
            else if (spl[0].StartsWith("QueueShow", StringComparison.CurrentCultureIgnoreCase) | spl[0].StartsWith("QueueUpdate", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (spl.Length < 15)
                        throw new Exception($"Invalid QueueShow or QueueUpdate message length.");

                    ID = spl[1];

                    if (ID.StartsWith("PICKUP"))
                    {
                        Type = Types.pickup;
                        if (int.TryParse(ID.Replace("PICKUP", ""), out int val))
                            Order = val;
                        else
                            throw new Exception($"Could not parse \"{ID.Replace("PICKUP", "")}\" to: Integer");

                    }
                    else if (ID.StartsWith("DROPOFF"))
                    {
                        Type = Types.dropoff;
                        if (int.TryParse(ID.Replace("DROPOFF", ""), out int val))
                            Order = val;
                        else
                            throw new Exception($"Could not parse \"{ID.Replace("DROPOFF", "")}\" to: Integer");
                    }
                    else
                    {
                        throw new Exception();
                    }

                    JobID = spl[2];

                    if (int.TryParse(spl[3], out int pri))
                        Priority = pri;
                    else
                        throw new Exception($"Could not parse \"{spl[3]}\" to: Integer");

                    if (Enum.TryParse(spl[4], out ARCLStatus status))
                        Status = status;
                    else
                        throw new Exception($"Could not parse \"{spl[4]}\" to: ARCLStatus");

                    if (!spl[5].StartsWith("ID_"))
                    {
                        if (Enum.TryParse(spl[5], out ARCLSubStatus subStatus))
                            SubStatus = subStatus;
                        else
                            throw new Exception($"Could not parse \"{spl[5]}\" to: ARCLSubStatus");
                    }

                    GoalName = spl[7].Replace("\"", "");

                    RobotName = spl[8].Replace("\"", "");

                    if (!spl[9].Equals("None"))
                    {
                        if (DateTime.TryParse($"{spl[9]} {spl[10]}", out DateTime dt))
                            StartedOn = dt;
                    }
                    else
                        throw new Exception($"Could not parse \"{spl[9]} {spl[10]}\" to: DateTime");

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
                        throw new Exception($"Could not parse \"{spl[i]}\" to: Integer");

                }
                catch (Exception ex)
                {
                    Exception = ex;
                    IsMessageExeption = true;
                }
                return;
            }
            else
            {
                Exception = new Exception("The message does not match a know pattern.");
                IsMessageExeption = true;
            }
        }

    }
    public class QueueManagerJob
    {
        public string ID { get; }
        public int SegmentCount => Segments.Count;

        public QueueManagerJobSegment CurrentSegment
        {
            get
            {
                if (Segments.Count > 0)
                {

                    foreach (KeyValuePair<string, QueueManagerJobSegment> que in Segments.OrderBy(x => x.Value.Order))
                    {
                        if (que.Value.Status != ARCLStatus.Completed)
                            return que.Value;
                    }
                    return null;
                }
                else
                    return null;
            }
        }
        public ReadOnlyConcurrentDictionary<string, QueueManagerJobSegment> Segments { get; private set; } = new ReadOnlyConcurrentDictionary<string, QueueManagerJobSegment>(10, 100);

        public ARCLStatus Status
        {
            get
            {
                if (Segments.Count > 0)
                {
                    foreach(KeyValuePair<string, QueueManagerJobSegment> que in Segments.OrderBy(x => x.Value.Order))
                    {
                        if (que.Value.Status != ARCLStatus.Completed)
                            return que.Value.Status;
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
                if (Segments.Count > 0)
                {
                    foreach (KeyValuePair<string, QueueManagerJobSegment> que in Segments.OrderBy(x => x.Value.Order))
                    {
                        if (que.Value.Status != ARCLStatus.Completed)
                            return que.Value.SubStatus;
                    }
                    return ARCLSubStatus.None;
                }
                else
                    return ARCLSubStatus.None;
            }
        }

        public QueueManagerJob(string id) => ID = id;

        public QueueManagerJob(QueueManagerJobSegment segment)
        {
            ID = segment.JobID;
            while (!Segments.TryAdd(segment.ID, segment)) { Segments.Locked = false; }
        }
        public void AddSegment(QueueManagerJobSegment segment)
        {
            while (!Segments.TryAdd(segment.ID, segment)) { Segments.Locked = false; };
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
