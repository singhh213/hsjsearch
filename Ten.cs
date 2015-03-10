using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClassLibrary2
{
    public class Ten : TableEntity
    {
        public Ten(string link)
        {
            this.PartitionKey = "lastten";
            this.RowKey = "lastten";

            this.Link = link;
        }

        public Ten() { }

        public string Link { get; set; }
    }
}
