using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARCLTypes;

namespace ARCL
{
    /// <summary>

    /// The intention of this class is to process ARCL messages and fire events based on the type of message.
    /// All messsages are parsed by the respective EventArgs classes and passed with the event.
    /// Inherits: SocketManager
    /// Hides: Connect(), Write(), ConnectState Event
    /// Extends: Password, Login()
    /// </summary>
    public class ARCLConnection : AsyncSocket.ASocketManager
    {
        /// <summary>
        /// Raised when the connection to the ARCL Server changes.
        /// </summary>
        //public new event ConnectedEventHandler ConnectState;
        public event EventHandler LoggedInEvent;
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

        public bool IsLoggedIn { get; private set; }

        private string Password;

        public ARCLConnection() : base() { }
        /// <summary>
        /// Connect and Login to an ARCL server.
        /// Hides ASocketManager.Connect()
        /// </summary>
        /// <param name="timeout">How long to wait for a connection.</param>
        /// <returns>Conenction or Login failed / succeeded</returns>
        public bool Connect(string host, int port, string password, int timeout = 5000)
        {
            return Connect(new ARCLConnectionSettings($"{host}:{port}:{password}"), timeout);
        }
        public bool Connect(ARCLConnectionSettings settings, int timeout = 5000)
        {
            if (base.IsConnected & IsLoggedIn) return true;

            Password = settings.Password;

            base.ExceptionEvent -= ARCLConnection_ExceptionEvent;
            base.ExceptionEvent += ARCLConnection_ExceptionEvent;

            base.CloseEvent -= ARCLConnection_CloseEvent;
            base.CloseEvent += ARCLConnection_CloseEvent;

            if (base.Connect(settings, timeout))
            {
                StartReceiveAsync();

                int start = Environment.TickCount;
                while (!IsLoggedIn)
                {
                    if ((Environment.TickCount - start) > timeout)
                        break;
                    if (!base.IsConnected)
                        break;
                }
                if (!IsLoggedIn)
                {
                    HandleException(new Exception("Could not login."));
                    base.Close();
                    return false;
                }
                return true;
            }
            return false;
        }

        private void ARCLConnection_CloseEvent()
        {
            IsLoggedIn = false;
        }

        private void ARCLConnection_ExceptionEvent(object sender, EventArgs e)
        {
            IsLoggedIn = false;
        }

        public void StartReceiveAsync()
        {
            if (base.IsReceiving)
                return;

            base.MessageEvent -= ARCLConnection_MessageEvent;
            base.MessageEvent += ARCLConnection_MessageEvent;

            base.StartReceiveMessages("\r\n");
        }

        public new void Send(string msg) => base.Send(msg + "\r\n");

        //Private
        private void ARCLConnection_MessageEvent(object sender, EventArgs e)
        {
            string message = ((string)sender).Trim('\r', '\n');

            if (!IsLoggedIn)
            {
                if (message.StartsWith("Enter password:"))
                {
                    Send(Password);
                    return;
                }
                if (message.Contains("End of commands"))
                {
                    IsLoggedIn = true;

                    Task.Run(() => LoggedInEvent?.Invoke(null, null));
                }
                return;
            }

            if (message.StartsWith("Status:"))
            {
                StatusUpdate?.Invoke(this, new StatusUpdateEventArgs(message)); ;
                return;
            }

            if (message.StartsWith("RangeDeviceGetCurrent:", StringComparison.CurrentCultureIgnoreCase))
            {
                RangeDeviceCurrentUpdate?.Invoke(this, new RangeDeviceReadingUpdateEventArgs(message));
                return;
            }

            if (message.StartsWith("RangeDeviceGetCumulative:", StringComparison.CurrentCultureIgnoreCase))
            {
                RangeDeviceCumulativeUpdate?.Invoke(this, new RangeDeviceReadingUpdateEventArgs(message));
                return;
            }

            if (message.StartsWith("QueueRobot", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndQueueShowRobot", StringComparison.CurrentCultureIgnoreCase))
            {
                QueueRobotUpdate?.Invoke(this, new QueueRobotUpdateEventArgs(message));
                return;
            }

            if (message.StartsWith("QueueShow", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndQueueShow", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("QueueUpdate", StringComparison.CurrentCultureIgnoreCase))
            {
                QueueJobUpdate?.Invoke(this, new QueueManagerJobSegment(message));
                return;
            }

            if ((message.StartsWith("ExtIO", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndExtIO", StringComparison.CurrentCultureIgnoreCase)) && !message.Contains("Needed"))
            {
                ExternalIOUpdate?.Invoke(this, new ExternalIOUpdateEventArgs(message));
                return;
            }
            if (Regex.Match(message, @"GetConfigSection", RegexOptions.IgnoreCase).Success || Regex.Match(message, @"Configuration changed", RegexOptions.IgnoreCase).Success)
            {
                ConfigSectionUpdate?.Invoke(this, new ConfigSectionUpdateEventArgs(message));
                return;
            }
            if (message.StartsWith("RangeDevice", StringComparison.CurrentCultureIgnoreCase) || message.StartsWith("EndOfRangeDeviceList", StringComparison.CurrentCultureIgnoreCase))
            {
                RangeDeviceUpdate?.Invoke(this, new RangeDeviceEventArgs(message));
                return;
            }
        }
    }
}