using System;
using System.Collections.Generic;
using System.Text;

namespace ARCLTypes
{
    public class QueueRobotUpdateEventArgs : EventArgs
    {
        //QueueRobot: "robotName" robotStatus robotSubstatus echoString
        public string Message { get; private set; }
        public string Name { get; private set; }
        public ARCLStatus Status { get; private set; }
        public ARCLSubStatus SubStatus { get; private set; }
        public string SubStatusCustomUser { get; private set; } = string.Empty;
        public bool IsEnd { get; private set; }
        public QueueRobotUpdateEventArgs(string msg)
        {
            if(msg.StartsWith("EndQueue", StringComparison.CurrentCultureIgnoreCase))
            {
                IsEnd = true;
                return;
            }

            Message = msg;

            string[] spl = msg.Split(' ');

            Name = spl[1].Trim('\"');

            if(Enum.TryParse(spl[2], out ARCLStatus status))
                Status = status;
            else
                throw new QueueRobotParseException();

            if(Enum.TryParse(spl[3], out ARCLSubStatus subStatus))
                SubStatus = subStatus;
            else
            {
                SubStatus = ARCLSubStatus.CustomUser;
                SubStatusCustomUser = spl[3];
            }
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
}
