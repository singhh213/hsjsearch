using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClassLibrary2
{
    public class Stats : TableEntity
    {
        public Stats(string state, string cpu, string ram, int totalUrls, 
            int? queueSize, int tableSize)
        {
            this.PartitionKey = "stats";
            this.RowKey = "stats";

            this.State = state;
            this.Cpu = cpu;
            this.Ram = ram;
            this.TotalUrls = totalUrls;
           
            this.QueueSize = queueSize;
            this.TableSize = tableSize;
            
        }

        public Stats() { }

        public string State { get; set; }
        public string Cpu { get; set; }
        public string Ram { get; set; }
        public int TotalUrls { get; set; }
        
        public int? QueueSize { get; set; }
        public int TableSize { get; set; }
        
    }
}
