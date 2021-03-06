﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ARCLTypes;

namespace ARCL
{
   public class QueueRobotManager
   {
      /// <summary>
      /// Raised when the Robots list is sycronized with the EM/LD robot queue.
      /// Raised when the connection is dropped.
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="state"></param>
      public delegate void InSyncUpdateEventHandler(object sender, bool state);
      /// <summary>
      /// Raised when the Robots list is sycronized with the EM/LD robot queue.
      /// Raised when the connection is dropped.
      /// </summary>
      public event InSyncUpdateEventHandler InSync;
      /// <summary>
      /// True when the Robots list is sycronized with the EM/LD robot queue.
      /// False when the connection is dropped.
      /// </summary>
      public bool IsSynced { get; private set; } = false;

      private ARCLConnection Connection { get; }

      /// <summary>
      /// Dictionary of Robots in the EM/LD queue.
      /// Not valid until InSync is true.
      /// </summary>
      public ReadOnlyConcurrentDictionary<string, QueueRobotUpdateEventArgs> Robots { get; set; } = new ReadOnlyConcurrentDictionary<string, QueueRobotUpdateEventArgs>(10, 100);

      public bool IsRobotAvailable => RobotsAvailable > 0;
      public int RobotsAvailable
      {
         get
         {
            if (!IsSynced) return 0;

            int cnt = 0;

            foreach (KeyValuePair<string, QueueRobotUpdateEventArgs> robot in Robots)
               if (robot.Value.Status == ARCLStatus.Available)
                  cnt++;
            return cnt;
         }
      }
      public int RobotsUnAvailable => Robots.Count() - RobotsAvailable;
      public bool IsRunning { get; private set; } = false;

      /// <summary>
      /// Instantiate the class and store the ARCLConnection ref.
      /// </summary>
      /// <param name="connection"></param>
      public QueueRobotManager(ARCLConnection connection) => Connection = connection;

      /// <summary>
      /// Clears the Robots dictionary.
      /// Calls StartReceiveAsync() on the ARCLConnection. **The connection must already be made.
      /// Initiates a QueueShowRobot command. 
      /// </summary>
      public void Start()
      {
         if (!Connection.IsConnected)
         {
            Stop();
            return;
         }

         Robots.Clear();

         Connection.ConnectState += Connection_ConnectState;
         Connection.QueueRobotUpdate += Connection_QueueRobotUpdate;

         if (!Connection.IsReceivingAsync)
            Connection.StartReceiveAsync();

         ThreadPool.QueueUserWorkItem(new WaitCallback(QueueShowRobotThread));
      }
      /// <summary>
      /// InSync is set to false.
      /// Calls StopReceiveAsync() on the ARCLConnection 
      /// </summary>
      public void Stop()
      {
         if (IsSynced)
         {
            IsSynced = false;
            Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, false)));
         }

         Connection.ConnectState -= Connection_ConnectState;
         Connection.QueueRobotUpdate -= Connection_QueueRobotUpdate;

         Connection.StopReceiveAsync();
      }

      private void Connection_ConnectState(object sender, bool state)
      {
         if (!state)
            Stop();
      }
      private void Connection_QueueRobotUpdate(object sender, QueueRobotUpdateEventArgs data)
      {
         if (data.IsEnd)
         {
            if (!IsSynced)
            {
               IsSynced = true;
               Connection.QueueTask(false, new Action(() => InSync?.Invoke(this, true)));
            }
            return;
         }

         if (!Robots.ContainsKey(data.Name))
         {
            while (!Robots.TryAdd(data.Name, data)) { Robots.Locked = false; }

            if (IsSynced)
               IsSynced = false;
         }
         else
            Robots[data.Name] = data;
      }
      private void QueueShowRobot() => Connection.Write("queueShowRobot");
      private void QueueShowRobotThread(object sender)
      {
         while (!Connection.IsReceivingAsync) { };

         try
         {
            IsRunning = true;

            QueueShowRobot();

            Stopwatch sw = new Stopwatch();

            sw.Restart();
            while (sw.ElapsedMilliseconds < 1000)
            {
               if (!Connection.IsReceivingAsync)
               {
                  IsRunning = false;
                  return;
               }
               Thread.Sleep(10);
            }
         }
         catch
         {
            IsRunning = false;
         }
         finally
         {
            if (IsRunning)
               ThreadPool.QueueUserWorkItem(new WaitCallback(QueueShowRobotThread));
         }
      }
   }
}
