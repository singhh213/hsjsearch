using HtmlAgilityPack;
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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace ClassLibrary2
{
    public class Crawler
    {
        public static List<string> sitemaps = new List<string>();
        private static List<string> cnnDisallows = new List<string>();
        private static List<string> brDisallows = new List<string>();
        private static HashSet<string> validLinks = new HashSet<string>();
        public static CloudQueue queue;
        public static CloudTable table;
        public static CloudBlockBlob fileblob;
        public static CloudQueue commands;
        public static int tableCounter = 0;
        public static int errorCounter = 0;
        private static List<string> tenLast = new List<string>();
        private static int tenCounter = 0;
        private static readonly string cnnLink = "http://www.cnn.com";
        private static readonly string brLink = "http://www.bleacherreport.com";

        public static void Initialize()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("datablob");
            fileblob = container.GetBlockBlobReference("wiki.txt");
            queue = queueClient.GetQueueReference("crawlurls");
            queue.CreateIfNotExists();
            table = tableClient.GetTableReference("pagetitles");
            table.CreateIfNotExists();
            commands = queueClient.GetQueueReference("commands");
            commands.CreateIfNotExists();
        }

        public static void buildRobots(string url, string keyword)
        {
            WebRequest request = WebRequest.Create(url);
            using (WebResponse response = request.GetResponse())
            {
                StreamReader responseReader = new StreamReader(response.GetResponseStream());

                while (responseReader.Peek() >= 0)
                {
                    string responseData = responseReader.ReadLine();

                    if (responseData.Contains(keyword))
                    {
                        sitemaps.Add(responseData.Substring(9));
                    }

                    if (responseData.Contains("Disallow"))
                    {
                        buildDisallow(url, responseData);
                    }
                }
            }
        }

        private static void buildDisallow(string url, string responseData)
        {
            if (url.Contains("bleacherreport"))
            {
                brDisallows.Add(responseData.Substring(10));
            }
            else
            {
                cnnDisallows.Add(responseData.Substring(10));
            }
        }

        public static void parseSitemaps(string map)
        {
            using (XmlReader reader = XmlReader.Create(map))
            {
                while (reader.Read())
                {
                    if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "loc"))
                    {
                        string url = reader.ReadElementContentAsString();

                        if (url.Contains("xml"))
                        {
                            if (url.Contains("2015"))
                            {
                                parseSitemaps(url);
                            }
                        }
                        else
                        {
                            CloudQueueMessage link = new CloudQueueMessage(url);
                            queue.AddMessageAsync(link);
                            validLinks.Add(url);
                        }
                    }
                }
            }
        }

        public static void ParseHtml(string link)
        {
            try
            {
                var request = HttpWebRequest.Create(link);
                request.Proxy = null;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (HttpStatusCode.OK == response.StatusCode)
                    {
                        var getHtmlWeb = new HtmlWeb();

                        var document = getHtmlWeb.Load(link);
                        var aTags = document.DocumentNode.SelectNodes("//a[@href]");
                        var titleTag = document.DocumentNode.SelectSingleNode("//title");
                        var dateTag = document.DocumentNode.SelectSingleNode("//meta[@name='pubdate']");
                        var bodyTag = document.DocumentNode.SelectSingleNode("//body");

                        var pNode = bodyTag.SelectNodes(".//p");

                        string text = BodyText(pNode);
                        
                        string date = CheckDate(dateTag);

                        string title = titleTag.InnerHtml.Trim();

                        string title2 = title.ToLower();

                        var titleWords = title2.Split(' ');

                        foreach (var wd in titleWords)
                        {
                            var word = wd.Trim(new Char[] {' ', '*', ',', '-', '!', '(', ')', '?', '|', '\'', ':',
                                                            '#', '\"', '+'});
                            if (!word.Equals(""))
                            {
                                var page = new Url(word, date, link, text.Trim(), title, 0);
                                TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(page);
                                table.ExecuteAsync(insertOrReplaceOperation);
                                tableCounter++;
                            }
                        }

                        UpdateLastTen(link);

                        CheckLinks(aTags, link);
                    }
                }
            }
            catch (WebException e)
            {
                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;

                    if (httpResponse != null)
                    {
                        string code = httpResponse.StatusCode.ToString();
                        InsertError(link, code);
                    }
                }
            }
            catch (Exception e)
            {
                InsertError(link, e.Message);
            }
        }

        public static void Rank(string input)
        {
            var getHtmlWeb = new HtmlWeb();

            var document = getHtmlWeb.Load(input);
            var titleTag = document.DocumentNode.SelectSingleNode("//title");

            string title = titleTag.InnerHtml.Trim().ToLower();

            var titleWords = title.Split(' ');

            foreach (var wd in titleWords)
            {
                var word = wd.Trim(new Char[] {' ', '*', ',', '-', '!', '(', ')', '?', '|', '\'', ':',
                                                            '#', '\"', '+'});
                if (!word.Equals(""))
                {

                    TableOperation retrieveOperation = TableOperation.Retrieve<Url>(HttpUtility.UrlEncode(word), HttpUtility.UrlEncode(input));

                   
                    TableResult retrievedResult = table.Execute(retrieveOperation);
                    Url entity = (Url)retrievedResult.Result;
                    int currRank = entity.Rank;

                    var entity2 = new DynamicTableEntity(HttpUtility.UrlEncode(word), HttpUtility.UrlEncode(input));

                    var properties = new Dictionary<string, EntityProperty>();
                    properties.Add("Rank", new EntityProperty(currRank + 1));


                    entity2.Properties = properties;
                    entity2.ETag = "*";

                    var mergeOperation = TableOperation.Merge(entity2);
                    table.Execute(mergeOperation);
                }
            }

        }

        public static void UpdateLastTen(string link)
        {
            if (tenCounter != 10)
            {
                tenLast.Add(link);
                tenCounter++;
            }
            else
            {
                tenLast.RemoveAt(0);
                tenLast.Add(link);
            }

            string csv = string.Join(",", tenLast.ToArray());
            var count = new Ten(csv);
            TableOperation insertOperation2 = TableOperation.InsertOrReplace(count);
            table.ExecuteAsync(insertOperation2);
        }

        public static void Clear()
        {
            tableCounter = 0;
            errorCounter = 0;
            tenCounter = 0;
            validLinks = new HashSet<string>();
            sitemaps.Clear();
            brDisallows.Clear();
            cnnDisallows.Clear();
            tenLast.Clear();
        }

        public static string BodyText(HtmlNodeCollection pNode)
        {
            string text = "";
            if (pNode != null)
            {
                foreach (var node in pNode)
                {
                    text = text + " " + node.InnerText;
                }
            }
            return text;
        }

        public static void InsertError(string link, string code)
        {
            var errorPage = new ErrorUrl(link, code);
            TableOperation insertError = TableOperation.Insert(errorPage);
            table.ExecuteAsync(insertError);
            errorCounter++;
        }

        public static string CheckDate(HtmlNode dateTag)
        {
            if (dateTag == null)
            {
                return "not shown";
            }
            else
            {
                return dateTag.Attributes["content"].Value;
            }
        }

        public static void CheckLinks(HtmlNodeCollection aTags, string link)
        {
            if (aTags != null)
            {
                foreach (var aTag in aTags)
                {
                    string tagUrl = FixLink(aTag.Attributes["href"].Value, link);
                    
                    Boolean cnn = tagUrl.Contains("cnn.com/") || tagUrl.Contains(".cnn.com");

                    Boolean br = tagUrl.Contains("bleacherreport.com/");

                    if (cnn || br)
                    {
                        if (!validLinks.Contains(tagUrl))
                        {
                            if (IsAllowed(tagUrl, cnn, br))
                            {
                                CloudQueueMessage newUrl = new CloudQueueMessage(tagUrl);
                                queue.AddMessageAsync(newUrl);
                                validLinks.Add(tagUrl);
                            }
                        }
                    }
                }
            }
        }

        public static string FixLink(string tagUrl, string link)
        {
            if (tagUrl.StartsWith("//"))
            {
                tagUrl = "http:" + tagUrl;
            }
            else if (tagUrl.StartsWith("/"))
            {
                if (link.Contains("bleacherreport.com"))
                {
                    tagUrl = brLink + tagUrl;
                }
                else if (link.Contains("cnn.com"))
                {
                    tagUrl = cnnLink + tagUrl;
                }
            }
            return tagUrl;
        }

        public static Boolean IsAllowed(string tagUrl, Boolean cnn, Boolean br)
        {
            Boolean allowed = true;

            if (br)
            {
                foreach (string disallow in brDisallows)
                {
                    if (tagUrl.Contains(disallow))
                    {
                        allowed = false;
                        break;
                    }
                }
            }
            else
            {
                foreach (string disallow in cnnDisallows)
                {
                    if (tagUrl.Contains(disallow))
                    {
                        allowed = false;
                        break;
                    }
                }
            }
            return allowed;
        }
    }
}
