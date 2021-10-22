using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using ARCLTypes;
using SocketManagerNS;

namespace ARCL
{
    /// <summary>

    /// The intention of this class is to process ARCL messages and fire events based on the type of message.
    /// All messsages are parsed by the respective EventArgs classes and passed with the event.
    /// Inherits: SocketManager
    /// Hides: Connect(), Write(), ConnectState Event
    /// Extends: Password, Login()
    /// </summary>
    public class ARCLConnection : SocketManager
    {
        /// <summary>
        /// Raised when the connection to the ARCL Server changes.
        /// </summary>
        public new event ConnectedEventHandler ConnectState;

        /// <summary>
        /// A message that starts with "Queue" or "EndQueue" and does not contain "Robot".
        /// </summary>
        /// <param name="sender">Will always be "this".</param>
        /// <param name="data">Job information.</param>
        public delegate void QueueJobEventHandler(object sender, QueueManagerJobSegment data);
        /// <summary>
        /// A message that starts with "Queue" or "EndQueue" and does not contain "Robot".
        /// </summary>
        public event QueueJobEventHandler QueueJobUpdate;

        /// <summary>
        /// A message that starts with "Queue" or "EndQueue" and does contain "Robot".
        /// </summary>
        /// <param name="sender">Will always be "this".</param>
        /// <param name="data">Robot information.</param>
        public delegate void QueueRobotUpdateEventHandler(object sender, QueueRobotUpdateEventArgs data);
        /// <summary>
        /// A message that starts with "Queue" or "EndQueue" and does contain "Robot".
        /// </summary>
        public event QueueRobotUpdateEventHandler QueueRobotUpdate;

        public delegate void StatusUpdateEventHandler(object sender, StatusUpdateEventArgs data);
        public event StatusUpdateEventHandler StatusUpdate;

        public delegate void RangeDeviceCurrentUpdateEventHandler(object sender, RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCurrentUpdateEventHandler RangeDeviceCurrentUpdate;

        public delegate void RangeDeviceCumulativeUpdateEventHandler(object sender, RangeDeviceReadingUpdateEventArgs data);
        public event RangeDeviceCumulativeUpdateEventHandler RangeDeviceCumulativeUpdate;

        public delegate void RangeDeviceEventHandler(object sender, RangeDeviceEventArgs device);
        public event RangeDeviceEventHandler RangeDeviceUpdate;

        /// <summary>
        /// A message that starts with "ExtIO" or "EndExtIO".
        /// </summary>
        /// <param name="sender">Will always be "this".</param>
        /// <param name="data">External IO information.</param>
        public delegate void ExternalIOUpdateEventHandler(object sender, ExternalIOUpdateEventArgs data);
        /// <summary>
        /// A message that starts with "ExtIO" or "EndExtIO".
        /// </summary>
        public event ExternalIOUpdateEventHandler ExternalIOUpdate;

        public delegate void ConfigSectionUpdateEventHandler(object sender, ConfigSectionUpdateEventArgs data);
        public event ConfigSectionUpdateEventHandler ConfigSectionUpdate;

        public new ARCLConnectionSettings ConnectionSettings { get => (ARCLConnectionSettings)base.ConnectionSettings; set => base.ConnectionSettings = value; }

        //Public
        public ARCLConnection(ARCLConnectionSettings connectionSettings) : base(connectionSettings) { }
        public ARCLConnection() : base() { }
        /// <summary>
        /// Connect and Login to an ARCL server.
        /// Hides SocketManager.Connect()
        /// </summary>
        /// <param name="timeout">How long to wait for a connection.</param>
        /// <returns>Conenction or Login failed / succeeded</returns>
        public new bool Connect(int timeout = 3000)
        {
            if (base.Connect(timeout))
            {
                if (Login())
                {
                    base.ConnectState += ARCLConnection_ConnectState;

                    ConnectState?.Invoke(this, true);

                    return true;
                }
                else
                    base.Close();
            }
            else
                base.Close();

            ConnectState?.Invoke(this, false);
            return false;
        }
        public bool Connect(ARCLConnectionSettings connectionSettings, int timeout = 3000)
        {
            if (!ARCLConnectionSettings.ValidateConnectionString(connectionSettings.ConnectionString))
            {
                return false;
            }

            base.ConnectionSettings = connectionSettings;

            return this.Connect(timeout);
        }

        public new void Close()
        {
            base.DataReceived -= Connection_DataReceived;

            base.Close();
        }

        public new bool StartReceiveAsync(char messageTerminator = '\n')
        {
            if(IsReceivingAsync)
                return true;

            base.DataReceived += Connection_DataReceived;

            return base.StartReceiveAsync(messageTerminator);
        }

        public new void StopReceiveAsync(bool force = false)
        {
            base.DataReceived -= Connection_DataReceived;

            base.StopReceiveAsync(force);
        }

        private void ARCLConnection_ConnectState(object sender, bool state)
        {
            if (!state)
                base.ConnectState -= ARCLConnection_ConnectState;

            ConnectState?.Invoke(sender, state);
        }
        /// <summary>
        /// Writes the message to the ARCL server.
        /// Appends "\r\n" to the message.
        /// Hides SocketManager.Write()
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public new bool Write(string msg) => base.Write(msg + "\r\n");

        /// <summary>
        /// Send password and wait for "End of commands\r\n"
        /// </summary>
        /// <returns>Success/Fail</returns>
        private bool Login()
        {
            string msg = Read('\n');
            if (!msg.StartsWith("Enter password"))
                return false;

            Write(ConnectionSettings.Password);
            string rm = Read("End of commands\r\n");

            if (rm.EndsWith("End of commands\r\n")) return true;
            else return false;
        }



        //Private
        private void Connection_DataReceived(object sender, string data)
        {
            string[] messages = data.Split('\n');

            foreach(string msg in messages)
            {
                string message = msg.Trim('\r');

                //if (message.StartsWith("CommandError:"))
                //{
                //    this.QueueTask("CommandError", false, new Action(() => StatusUpdate?.Invoke(this, new StatusUpdateEventArgs(message)))); ;
                //    continue;
                //}

                if ((message.StartsWith("QueueRobot", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndQueueShowRobot", StringComparison.CurrentCultureIgnoreCase)))
                {
                    QueueRobotUpdate?.Invoke(this, new QueueRobotUpdateEventArgs(message));
                    continue;
                }

                if ((message.StartsWith("QueueShow", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndQueueShow", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("QueueUpdate", StringComparison.CurrentCultureIgnoreCase)))
                {
                    QueueJobUpdate?.Invoke(this, new QueueManagerJobSegment(message));
                    continue;
                }

                if ((message.StartsWith("ExtIO", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndExtIO", StringComparison.CurrentCultureIgnoreCase)) && !message.Contains("Needed"))
                {
                    ExternalIOUpdate?.Invoke(this, new ExternalIOUpdateEventArgs(message));
                    continue;
                }
                if(Regex.Match(message, @"GetConfigSection", RegexOptions.IgnoreCase).Success || Regex.Match(message, @"Configuration changed", RegexOptions.IgnoreCase).Success)
                {
                    ConfigSectionUpdate?.Invoke(this, new ConfigSectionUpdateEventArgs(message));
                    continue;
                }
                //if (message.StartsWith("GetConfigSection", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndOfGetConfigSection", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("Configuration changed", StringComparison.CurrentCultureIgnoreCase))
                //{

                //}
                //CommandError: getconfigsectionvalues General
                if (message.StartsWith("Status:"))
                {
                    StatusUpdate?.Invoke(this, new StatusUpdateEventArgs(message)); ;
                    continue;
                }

                if (message.StartsWith("RangeDeviceGetCurrent:", StringComparison.CurrentCultureIgnoreCase))
                {
                    RangeDeviceCurrentUpdate?.Invoke(this, new RangeDeviceReadingUpdateEventArgs(message));
                    continue;
                }

                if (message.StartsWith("RangeDeviceGetCumulative:", StringComparison.CurrentCultureIgnoreCase))
                {
                    RangeDeviceCumulativeUpdate?.Invoke(this, new RangeDeviceReadingUpdateEventArgs(message));
                    continue;
                }

                if (message.StartsWith("RangeDevice", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndOfRangeDeviceList", StringComparison.CurrentCultureIgnoreCase))
                {
                    RangeDeviceUpdate?.Invoke(this, new RangeDeviceEventArgs(message));
                    continue;
                }
            }

        }
    }
}