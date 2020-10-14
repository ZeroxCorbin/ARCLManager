# ARCLManager

## This is a C# dll to facilitate asycronous communications using the ARCL protocol.
# This repo is required for compalation;<br>
[SocketManager](https://github.com/ZeroxCorbin/SocketManager)<br>
# Here are some example applications.<br>
[Queue Job Manager Demo](https://github.com/ZeroxCorbin/ARCLManager_QueueJobManager_Demo)<br>
[Configuration Manager Demo](https://github.com/ZeroxCorbin/ARCLManager_ConfigurationManager_Demo)<br>


### Here is an example of using the ARCLConnection;

    using ARCL;
    using ARCLTypes;

    ARCLConnection Connection;

    private bool Connect()
    {
      string connectString = ARCLConnection.GenerateConnectionString("192.168.1.1", 7171, "adept");

      if (ARCLConnection.ValidateConnectionString(connectString))
      {
              Connection = new ARCLConnection(connectString);
      }
      else
      {
              return false;
      }

      if (Connection.Connect(true))
      {
              //Connected and logged in. Do work...

              return true;
      }
      else
      {
              return false;
      }
    }
	
# THIS IS A WORK IN PROGRESS.
