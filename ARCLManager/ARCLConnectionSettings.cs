using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ARCLTypes
{
    public class ARCLConnectionSettings : SocketManagerNS.ConnectionSettings
    {
        public static string GenerateConnectionString(string ip, int port, string password) => $"{ip}:{port}:{password}";
        public static string GenerateConnectionString(System.Net.IPAddress ip, int port, string password) => $"{ip}:{port}:{password}";
        public static new bool ValidateConnectionString(string connectionString)
        {
            if (connectionString.Count(c => c == ':') < 2) return false;
            string[] spl = connectionString.Split(':');

            if (!System.Net.IPAddress.TryParse(spl[0], out System.Net.IPAddress ip)) return false;

            if (!int.TryParse(spl[1], out int port)) return false;

            if (string.IsNullOrWhiteSpace(spl[2])) return false;

            return true;
        }

        public ARCLConnectionSettings(string connectionString) : base(connectionString) { }

        /// <summary>
        /// Gets the password portion of the connection string.
        /// Extends SocketManager.ConnectionString to support a password.
        /// </summary>
        public string Password
        {
            get
            {
                if (ConnectionString.Count(c => c == ':') < 2) return string.Empty;
                return ConnectionString.Split(':')[2];
            }
        }

    }
}
