using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClassLibrary2
{
    public class Url : TableEntity
    {
        public Url(string word, string date, string link, string text, string title, int rank)
        {
            this.PartitionKey = HttpUtility.UrlEncode(word);
            this.RowKey = HttpUtility.UrlEncode(link);

            this.Title = title;
            this.Date = date;
            this.Link = link;
            this.Text = text;
            this.Rank = rank;
        }

        public Url() { }

        public string Title { get; set; }
        public string Date { get; set; }
        public string Link { get; set; }
        public string Text { get; set; }
        public int Rank { get; set; }
    }
}
