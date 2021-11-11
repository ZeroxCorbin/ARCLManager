using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ARCLTypes
{
    public class ARCLConnectionSettings : AsyncSocket.ASocketSettings
    {
        public new bool IsConnectionStringValid => IsIPAddressValid & IsPortValid & IsPasswordValid;

        private bool GetPasswordString(out string pass)
        {
            pass = string.Empty;
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                if (ConnectionString.Count(c => c == ':') >= 2)
                {
                    pass = ConnectionString.Split(':')[2];
                    return !string.IsNullOrEmpty(pass);
                }
            }
            return false;
        }
        public string PasswordString { get { GetPasswordString(out string test); return test; } }
        public string Password => GetPasswordString(out string test) ? test : null;
        public bool IsPasswordValid => GetPasswordString(out string _);

        public ARCLConnectionSettings(string connectionString) : base(connectionString) { }
    }
}
