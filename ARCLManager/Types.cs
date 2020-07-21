using System;
using System.Collections.Concurrent;

namespace ARCLTypes
{

    //queueShow [echoString]
    //QueueRobot: <robotName> <robotStatus> <robotSubstatus> <echoString>
    //QueueRobot: "21" InProgress Driving ""
    //QueueShow: <id> <jobId> <priority> <status> <substatus> Goal<"goalName"> <”robotName”> <queued date> <queued time> <completed date> <completed time> <echoString> <failed count>
    //QueueShow: PICKUP3 JOB3 10 Completed None Goal "1" "21" 11/14/2012 11:49:23 11/14/2012 11:49:23 "" 0
    //EndQueueShow

    //queueMulti <number of goals> <number of fields per goal> <goal1> <goal1 args> <goal2> <goal2 args> … <goalN> <goalN args> [jobid]
    //queuemulti 4 2 x pickup 10 y pickup 19 z dropoff 20 t dropoff 20

    //QueueMulti: goal "x" with priority 10 id PICKUP1 and jobid JOB1 successfully queued
    //QueueMulti: goal<"goal1"> with priority<goal1_priority> id<PICKUPid_or_DROPOFFid> jobid<jobId> successfully queued
    //QueueMulti: goal<"goal2"> with priority<goal2_priority> id <PICKUPid_or_DROPOFFid> jobid<jobId> successfully queued and linked to <goal1_PICKUPid_or_DROPOFFid>
    //:
    //:
    //QueueMulti: goal<"goaln"> with priority<goaln_priority> id <PICKUPid_or_DROPOFFid> jobid<jobId> successfully queued and linked to <goal(n-1)_PICKUPid_or_DROPOFFid>
    //EndQueueMulti


    //queuePickupDropoff <goal1Name> <goal2Name> [priority1 or "default"] [priority2 or "default"] [jobId]
    //queuepickupdropoff goals<"goal1"> and<"goal2"> with priorities<priority1> and <priority2> ids<PICKUPid> and <DROPOFFid> jobId<jobId> successfully queued and linked to jobId<jobid>
    //QueueUpdate: <id> <jobId> <priority> <status=Pending> <substatus=ID_<id>> Goal <”goal2”>
    //             <robotName> <queued date> <queued time> <completed date = None > < completed time=None>
    //             <failed count>
    public class ReadOnlyConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue>
    {
        public bool Locked { get; set; } = true;
        public ReadOnlyConcurrentDictionary(int concurrencyLevel, int capacity) : base(concurrencyLevel, capacity) { }
        public new bool TryAdd(TKey key, TValue value)
        {
            if (Locked) return false;
            Locked = true;
            return base.TryAdd(key, value);
        }
        public new bool TryRemove(TKey key, out TValue value)
        {
            value = default;
            if (Locked) return false;
            Locked = true;
            return base.TryRemove(key, out value);
        }
    }

    public enum ARCLJobStatusRequestTypes
    {
        Pending,
        Interrupted,
        InProgress,
        Completed,
        Cancelled,
        Failed,
    }

    public enum ARCLStatus
    {
        Pending,
        Available,
        AvailableForJob,
        Interrupted,
        InProgress,
        Completed,
        Cancelling,
        Cancelled,
        BeforeModify,
        InterruptedByModify,
        AfterModify,
        UnAvailable,
        Failed,
        Loading
    }
    public enum ARCLSubStatus
    {
        None,
        AssignedRobotOffLine,
        NoMatchingRobotForLinkedJob,
        NoMatchingRobotForOtherSegment,
        NoMatchingRobot,
        ID_PICKUP,
        ID_DROPOFF,
        Available,
        AvailableForJob,
        Parking,
        Parked,
        DockParking,
        DockParked,
        UnAllocated,
        Allocated,
        BeforePickup,
        BeforeDropoff,
        BeforeEvery,
        Before,
        Buffering,
        Buffered,
        Driving,
        After,
        AfterEvery,
        AfterPickup,
        AfterDropoff,
        NotUsingEnterpriseManager,
        UnknownBatteryType,
        ForcedDocked,
        Lost,
        EStopPressed,
        Interrupted,
        InterruptedButNotYetIdle,
        OutgoingARCLConnLost,
        ModeIsLocked,
        Cancelled_by_MobilePlanner
    }




    public class QueueRobotUpdateEventArgs : EventArgs
    {
        //QueueRobot: "robotName" robotStatus robotSubstatus echoString
        public string Message { get; private set; }
        public string Name { get; private set; }
        public ARCLStatus Status { get; private set; }
        public ARCLSubStatus SubStatus { get; private set; }
        public bool IsEnd { get; private set; }
        public QueueRobotUpdateEventArgs(string msg)
        {
            if (msg.StartsWith("EndQueue", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }

            Message = msg;

            string[] spl = msg.Split(' ');

            Name = spl[1].Trim('\"');

            if (Enum.TryParse(spl[2], out ARCLStatus status))
                Status = status;
            else
                throw new QueueRobotParseException();

            if (Enum.TryParse(spl[3], out ARCLSubStatus subStatus))
                SubStatus = subStatus;
            else
                throw new QueueRobotParseException();
        }
    }

    public class QueueRobotParseException : Exception
    {
        public QueueRobotParseException()
        {
        }
        public QueueRobotParseException(string message) : base(message)
        {
        }
    }
    public class StatusUpdateEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DockingState { get; set; } = string.Empty;
        public string ForcedState { get; set; } = string.Empty;
        public float ChargeState { get; set; }
        public float StateOfCharge { get; set; }
        public string Location { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Heading { get; set; }
        public float Temperature { get; set; }

        public float Timestamp { get; set; }

        public StatusUpdateEventArgs(string msg, bool isReplay = false)
        {
            if (isReplay)
            {
                //0.361,encoderTransform,82849.2 -33808.8 140.02
                Message = msg;
                string[] spl = msg.Split(',');
                string[] loc = spl[2].Split();

                Location = String.Format("{0},{1},{2}", loc[0], loc[1], loc[2]);

                X = float.Parse(loc[0]);
                Y = float.Parse(loc[1]);
                Heading = float.Parse(loc[2]);

                if (float.TryParse(spl[0], out float res))
                {
                    Timestamp = res;
                }
                else
                {
                    if (!spl[0].Equals("starting"))
                    {

                    }
                }

            }
            else
            {
                Message = msg;
                string[] spl = msg.Split();
                int i = 0;
                float val;

                while (true)
                {
                    switch (spl[i])
                    {
                        case "Status:":
                            while (true)
                            {
                                if (spl[i + 1].Contains(":") & !spl[i + 1].Contains("Error")) break;
                                Status += spl[++i] + ' ';
                            }
                            break;

                        case "DockingState:":
                            if (!spl[i + 1].Contains(":"))
                                DockingState = spl[++i];
                            break;

                        case "ForcedState:":
                            if (!spl[i + 1].Contains(":"))
                                ForcedState = spl[++i];
                            break;

                        case "ChargeState:":
                            if (!spl[i + 1].Contains(":"))
                                if (float.TryParse(spl[++i], out val))
                                    ChargeState = val;
                            break;

                        case "StateOfCharge:":
                            if (!spl[i + 1].Contains(":"))
                                if (float.TryParse(spl[++i], out val))
                                    StateOfCharge = val;
                            break;

                        case "Location:":
                            if (!spl[i + 1].Contains(":"))
                            {
                                Location = String.Format("{0},{1},{2}", spl[++i], spl[++i], spl[++i]);
                                string[] spl1 = Location.Split(',');
                                X = float.Parse(spl1[0]);
                                Y = float.Parse(spl1[1]);
                                Heading = float.Parse(spl1[2]);
                            }

                            break;
                        case "Temperature:":
                            if (!spl[i + 1].Contains(":"))
                                if (float.TryParse(spl[++i], out val))
                                    Temperature = val;
                            break;

                        default:
                            break;
                    }

                    i++;
                    if (spl.Length == i) break;
                }
            }

        }
    }

    public class ExtIOUpdateParseException : Exception
    {
        public ExtIOUpdateParseException()
        {
        }

        public ExtIOUpdateParseException(string message)
            : base(message)
        {
        }
    }





}
