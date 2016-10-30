using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using IGL.Service;

namespace IGL.ServiceRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("IGL.ServiceRole is running");

            try
            {
                ServiceBusMessagingFactory.ConnectionString = "Endpoint=sb://[namespace].servicebus.windows.net/;SharedAccessKeyName=[keyname];SharedAccessKey=[key]";

                var workers = new List<Task>();

                RoleTaskRunner.OnGamePacketCompleted += RoleTaskRunner_OnGamePacketCompleted;
                RoleTaskRunner.OnListenerError += RoleTaskRunner_OnListenerError;

                workers.Add(RoleTaskRunner.RunAsync<EchoTask>(cancellationTokenSource.Token, 
                                                              "Echo", 
                                                              1,
                                                              new TimeSpan(0, 0, 30)));

                Task.WaitAll(workers.ToArray());                
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        private void RoleTaskRunner_OnListenerError(object sender, EventArgs e)
        {
            Console.WriteLine("Fail");
        }

        private void RoleTaskRunner_OnGamePacketCompleted(object sender, GamePacketArgs e)
        {
            Console.WriteLine("Completed");
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("IGL.ServiceRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("IGL.ServiceRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("IGL.ServiceRole has stopped");
        }
    }
}
