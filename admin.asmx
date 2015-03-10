using ClassLibrary2;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService1
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {

        private static Trie trie;
        private static string filePath;
        private static Dictionary<string, List<string>> cache = new Dictionary<string, List<string>>();
        private static int cacheCounter = 0;
        private static string lastTitle = "";
        private static int titleCounter = 0;
        private static string searchText = "";

        public WebService1()
        {
            Crawler.Initialize();
        }

        [WebMethod]
        public string DownloadBlob()
        {

            filePath = System.IO.Path.GetTempPath() + "\\wiki.txt";
            using (var fileStream = System.IO.File.OpenWrite(filePath))
            {

                Crawler.fileblob.DownloadToStream(fileStream);
            }
            string life = filePath;
            return life;
        }

        [WebMethod]
        public string BuildTrie()
        {
            PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");

            float space = memProcess.NextValue();
            trie = new Trie();

            using (StreamReader sr = new StreamReader(filePath))
            {
               
                while (sr.EndOfStream == false && (space > 50) )
                {
                    string line = sr.ReadLine();
                    trie.Add(line);
                    titleCounter++;
                    lastTitle = line;
                    space = memProcess.NextValue();
                }
            }
            return lastTitle;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> AutoComplete(string str)
        {
            if (str != null && trie != null)
            {
                return trie.Match(str, 10);
            }
            else
            {
                return null;
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string StartCrawling()
        {
            CloudQueueMessage start = new CloudQueueMessage("start");
            Crawler.commands.AddMessage(start);
            return "Crawling has begun";
        }

        [WebMethod]
        public string StopCrawling()
        {
            CloudQueueMessage stop = new CloudQueueMessage("stop");
            Crawler.commands.AddMessage(stop);
            return "Crawling has stopped";
        }

        [WebMethod]
        public string ClearIndex()
        {
            Crawler.table.DeleteIfExists();
            return "Index is currently clearing. You may start crawling again after 60 seconds (the start button will become active). Please do not refresh the dashboard while the index is clearing.";

        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> GetResults(string input)
        {
            List<string> resultsList = new List<string>();
            searchText = input;
            if (Crawler.table.Exists())
            {
                if (cache.ContainsKey(input))
                {
                    return cache[input];
                }
                else
                {
                    if (cacheCounter >= 100)
                    {
                        cacheCounter = 0;
                        cache.Clear();
                    }

                    var words = input.Split(' ');

                    List<Url> query = new List<Url>();
                    foreach (var word in words)
                    {

                        TableQuery<Url> wordQuery = new TableQuery<Url>()
                            .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, HttpUtility.UrlEncode(word))
                            );

                        var results = Crawler.table.ExecuteQuery(wordQuery);
                        query.AddRange(results);
                    }

                    var finalResults = query
                        .GroupBy(x => x.RowKey)
                        .Select(x => new Tuple<string, int, string, string, string>(x.Key, x.ToList().Count + x.ToList().First().Rank, x.ToList().First().Date,
                                x.ToList().First().Text, x.ToList().First().Title))
                        .OrderByDescending(x => x.Item2)
                        .ThenByDescending(x => x.Item3)
                        .Take(10);

                    foreach (var g in finalResults)
                    {
                        string url = HttpUtility.UrlDecode(g.Item1);
                        string bodyText = g.Item4;
                        if (bodyText.Length > 150)
                        {
                            bodyText = bodyText.Substring(0, 150);
                        }

                        StringBuilder b = new StringBuilder(bodyText.ToLower());

                        foreach (var word in words) {
                            b.Replace(word, "<strong>" + word + "</strong>");
                        
                        }

                        resultsList.Add(g.Item5);
                        resultsList.Add("..." + b.ToString() + "...");
                        resultsList.Add(url);
                    }

                    cache.Add(input, resultsList);
                    cacheCounter += 1;
                }
            }
            return resultsList;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void AddRank(string input)
        {
            Crawler.Rank(input);
            cache.Remove(searchText);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetPageTitle(string input)
        {

            if (Crawler.table.Exists())
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<Url>("url", HttpUtility.UrlEncode(input));
                TableResult retrievedResult = Crawler.table.Execute(retrieveOperation);

                if ((Url)retrievedResult.Result != null)
                {
                    string source = ((Url)retrievedResult.Result).Text;

                    if (source.Length > 150) {
                        source = source.Substring(0, 150);
                    }

                    return "Title: " + ((Url)retrievedResult.Result).Title + "<br/>" + "<br/>" + "Body Text: " + source;
                }
            }
            return "No page found";
        }

        [WebMethod]
        public string ClearUrlQueue()
        {
            Crawler.queue.Clear();
            Crawler.commands.Clear();
            return "The url queue has been cleared";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> LastTen()
        {
            List<string> last = new List<string>();

            if (Crawler.table.Exists())
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<Ten>("lastten", "lastten");
                TableResult retrievedResult = Crawler.table.Execute(retrieveOperation);

                Ten ten = (Ten)retrievedResult.Result;

                if (ten == null)
                {
                    return last;
                }
                else
                {
                    string csv = ten.Link;
                    string[] parts = csv.Split(',');
                    List<string> list = new List<string>(parts);
                    return list;
                }
            }
            return last;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> ListErrors()
        {
            List<string> errorsList = new List<string>();

            if (Crawler.table.Exists())
            {

                string rowKeyToUse = string.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);

                var results = (from g in Crawler.table.CreateQuery<ErrorUrl>()
                               where g.PartitionKey == "error"
                               && g.RowKey.CompareTo(rowKeyToUse) > 0
                               select g).Take(10);

                if (results != null)
                {
                    foreach (ErrorUrl entity in results)
                    {
                        errorsList.Add(entity.Error + " error at " + entity.Link);
                    }
                }
            }
            return errorsList;
        } 

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<object> Statistics()
        {
            List<object> stats = new List<object>();
            if (Crawler.table.Exists())
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<Stats>("stats", "stats");
                TableResult retrievedResult = Crawler.table.Execute(retrieveOperation);

                Stats newStats = (Stats)retrievedResult.Result;
                if (newStats != null)
                {
                    stats.Add(newStats.State);
                    stats.Add(newStats.Cpu);
                    stats.Add(newStats.Ram);
                    stats.Add(newStats.TotalUrls);
                    stats.Add(newStats.QueueSize);
                    stats.Add(newStats.TableSize);
                    stats.Add(titleCounter);
                    stats.Add(lastTitle);
                }
            }

            return stats;
        }
    }
}
