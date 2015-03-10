using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClassLibrary2
{
    public class ErrorUrl : TableEntity
    {
        public ErrorUrl(string link, string error)
        {
            this.PartitionKey = "error";
            this.RowKey = string.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks); ;

            this.Link = link;
            this.Error = error;
        }

        public ErrorUrl() { }

        public string Link { get; set; }
        public string Error { get; set; }
    }
}
