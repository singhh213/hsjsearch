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
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;
using System.Xml;
using Microsoft.WindowsAzure.Storage.Table;
using HtmlAgilityPack;
using ClassLibrary2;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        private PerformanceCounter cpuCounter = new PerformanceCounter();
        private Boolean loading = false;
        private Boolean crawl = false;

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 is running");

            Crawler.Initialize();

            while (true)
            {
                Thread.Sleep(50);
                
                if (Crawler.commands.PeekMessage() != null)
                {
                    CloudQueueMessage message = Crawler.commands.GetMessage();
                    if (message.AsString.Equals("start"))
                    {
                        Crawler.Initialize();
                        loading = true;
                        GetStats();
                        Crawler.buildRobots("http://www.bleacherreport.com/robots.txt", "nba");
                        Crawler.buildRobots("http://www.cnn.com/robots.txt", "Sitemap");

                        foreach (string map in Crawler.sitemaps)
                        {
                            Crawler.parseSitemaps(map);
                        }
                        crawl = true;
                        loading = false;
                    }
                    else
                    {
                        crawl = false;
                        Crawler.Clear();
                    }
                    Crawler.commands.DeleteMessage(message);
                }

                if (crawl)
                {
                    GetStats();
                    CloudQueueMessage message = Crawler.queue.GetMessage();
                    
                    if (message != null)
                    {
                        string link = message.AsString;
                        Crawler.ParseHtml(link);
                        Crawler.queue.DeleteMessageAsync(message);
                    }  
                }
            }
        
            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }


        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }

        public void GetStats()
        {
            string state;

            if (!crawl && loading)
            {
                state = "loading";
            }
            else if (crawl && Crawler.queue.PeekMessage() == null && !loading)
            {
                state = "idle";
            }
            else if (!crawl)
            {
                state = "stopped";
            }
            else
            {
                state = "crawling";
            }

            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";

            string cpu = cpuCounter.NextValue() + "%";

            string ram = ramCounter.NextValue() + "MB";

            int crawledUrls = Crawler.tableCounter;

            int totalUrls = crawledUrls + Crawler.errorCounter;

            Crawler.queue.FetchAttributes();

            int? queueSize = Crawler.queue.ApproximateMessageCount;

            int tableSize = crawledUrls;

            var crawlStats = new Stats(state, cpu, ram, totalUrls, queueSize, tableSize);
            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(crawlStats);
            Crawler.table.ExecuteAsync(insertOrReplaceOperation);
        }
    }
}
