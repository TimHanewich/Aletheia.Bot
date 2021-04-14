using System;
using Aletheia;
using Aletheia.Fundamentals;
using Aletheia.Service;
using Aletheia.Service.Fundamentals;
using QuickChart;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using TimHanewich.Twitter;

namespace Aletheia.Bot
{
    public class AletheiaBotService
    {
        private string apikey;
        private string AzureStorageConnectionString;
        private string TwitterBearerToken;

        public AletheiaBotService(string aletheia_api_key, string azure_storage_con_str, string twitter_bearer)
        {
            apikey = aletheia_api_key;
            AzureStorageConnectionString = azure_storage_con_str;
            TwitterBearerToken = twitter_bearer;
        }

        public async Task<KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>[]> GetFactsForChartRequestAsync(ChartingRequest request)
        {
            AletheiaService AlServ = new AletheiaService(apikey);
            List<KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>> ToReturn = new List<KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>>();
            foreach (FactLabel label in request.FactLabels)
            {
                FinancialFactTrendRequest trreq = new FinancialFactTrendRequest();
                trreq.Id = request.CompanyId;
                trreq.Label = label;
                trreq.PeriodType = request.PeriodType;
                trreq.After = request.After;
                trreq.Before = request.Before;
                FinancialFactTrendDataPoint[] points = await AlServ.FinancialFactTrendAsync(trreq);
                ToReturn.Add(new KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>(label, points));
            }
            return ToReturn.ToArray();
        }
    
        public string AssembleQuickChartRequestUrlFromData(KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>[] data_points, int width, int height)
        {
            //First get a list of all dates
            List<DateTime> AllDates = new List<DateTime>();
            foreach (KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]> classification in data_points)
            {
                foreach (FinancialFactTrendDataPoint point in classification.Value)
                {
                    if (AllDates.Contains(point.PeriodEnd) == false)
                    {
                        AllDates.Add(point.PeriodEnd);
                    }
                }
            }

            //Arrange all dates in order from oldest to newest
            List<DateTime> AllDates_Ordered = new List<DateTime>();
            while (AllDates.Count > 0)
            {
                DateTime Winner = AllDates[0];
                foreach (DateTime dt in AllDates)
                {
                    if (dt < Winner)
                    {
                        Winner = dt;
                    }
                }
                AllDates_Ordered.Add(Winner);
                AllDates.Remove(Winner);
            }
            AllDates = AllDates_Ordered;

            //Now that we have all dates, assemble these into an array which will be the X-Axis value label
            List<string> XLabel = new List<string>();
            foreach (DateTime dt in AllDates)
            {
                XLabel.Add(dt.ToShortDateString());
            }

            //Now assemble each data set
            List<JObject> DataSets = new List<JObject>();
            foreach (KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]> point in data_points)
            {
                JObject this_data_set = new JObject();

                //Add the label
                this_data_set.Add("label", point.Key.ToString());

                //Assemble the data for this
                string data_piece = "[";
                foreach (DateTime dt in AllDates)
                {
                    bool HaveFactForThisOne = false;
                    foreach (FinancialFactTrendDataPoint datapoint in point.Value)
                    {
                        if (datapoint.PeriodEnd == dt)
                        {
                            data_piece = data_piece + datapoint.Value.ToString("0") + ",";
                            HaveFactForThisOne = true;
                        }
                    }
                    if (HaveFactForThisOne == false)
                    {
                        data_piece = data_piece + " " + ","; //Blank!
                    }
                }
                data_piece = data_piece.Substring(0, data_piece.Length - 1); //Remove the trailing comma
                data_piece = data_piece + "]"; //Add the close to the end

                //Plug in the data
                this_data_set.Add("data", JArray.Parse(data_piece));

                DataSets.Add(this_data_set);
            }


            //ASSEMBLE THE REQUEST
            Chart qc = new Chart();
            qc.Width = width;
            qc.Height = height;
            qc.BackgroundColor = "white";
            JObject jo = new JObject();
            jo.Add("type", "line");
            
            //Add data
            JObject jo_data = new JObject();
            jo.Add("data", jo_data);
            jo_data.Add("labels", JArray.Parse(JsonConvert.SerializeObject(XLabel.ToArray())));
            jo_data.Add("datasets", JArray.Parse(JsonConvert.SerializeObject(DataSets.ToArray())));

            //Add options
            JObject jo_options = new JObject();
            JObject jo_plugins = new JObject();
            jo_options.Add("plugins", jo_plugins);
            JObject jo_tickFormat = new JObject();
            jo_plugins.Add("tickFormat", jo_tickFormat);
            jo_tickFormat.Add("locale", "en-US");
            jo_tickFormat.Add("useGrouping", true);
            jo.Add("options", jo_options);
            
            //Tack on the config
            qc.Config = jo.ToString();


            return qc.GetUrl();
        }
    
        public async Task<Stream> DownloadQuickChartImageAsync(string url)
        {
            HttpClient hc = new HttpClient();
            HttpResponseMessage resp = await hc.GetAsync(url);
            Stream s = await resp.Content.ReadAsStreamAsync();
            return s;
        }
    
        public async Task<DateTimeOffset> GetMostRecentlyProcessedTweetProcessedAtAsync()
        {
            CloudStorageAccount csa;
            CloudStorageAccount.TryParse(AzureStorageConnectionString, out csa);

            CloudBlobClient cbc = csa.CreateCloudBlobClient();
            CloudBlobContainer cont = cbc.GetContainerReference("general");
            await cont.CreateIfNotExistsAsync();

            //Get the blob
            CloudBlockBlob MostRecentlyProcessedTweetProcessedAt = cont.GetBlockBlobReference("MostRecentlyProcessedTweetProcessedAt");
            if (MostRecentlyProcessedTweetProcessedAt.Exists())
            {
                string content = await MostRecentlyProcessedTweetProcessedAt.DownloadTextAsync();
                try
                {
                    DateTimeOffset dt = DateTimeOffset.Parse(content);
                    return dt;
                }
                catch
                {
                    return new DateTime(2000, 1, 1);
                }
            }
            else
            {
                return new DateTime(2000, 1, 1);
            }
        }
    
        public async Task UploadMostRecentlyProcessedTweetProcessedAtAsync(DateTimeOffset most_recent)
        {
            CloudStorageAccount csa;
            CloudStorageAccount.TryParse(AzureStorageConnectionString, out csa);

            CloudBlobClient cbc = csa.CreateCloudBlobClient();
            CloudBlobContainer cont = cbc.GetContainerReference("general");
            await cont.CreateIfNotExistsAsync();

            CloudBlockBlob MostRecentlyProcessedTweetProcessedAt = cont.GetBlockBlobReference("MostRecentlyProcessedTweetProcessedAt");
            await MostRecentlyProcessedTweetProcessedAt.UploadTextAsync(most_recent.ToString());
        }
    
        public async Task<Tweet[]> ObserveNewTweetMentionsAsync(string search_term)
        {
            List<Tweet> ToReturn = new List<Tweet>();
            DateTimeOffset LastObservedAt = await GetMostRecentlyProcessedTweetProcessedAtAsync();
            
            //Get the tweets
            TwitterService ts = new TwitterService(TwitterBearerToken);
            RecentSearch rs = await ts.RecentSearchAsync(search_term, 25, null, new TweetField[] {TweetField.CreatedAt, TweetField.AuthorId});
            
            if (rs.Tweets != null)
            {
                if (rs.Tweets.Length > 0)
                {

                    //Collect the ones that apply
                    foreach (Tweet t in rs.Tweets)
                    {
                        if (t.CreatedAt.Value > LastObservedAt)
                        {
                            ToReturn.Add(t);
                        }
                    }
                    
                    //Get the newest
                    DateTimeOffset newest = rs.Tweets[0].CreatedAt.Value;
                    foreach (Tweet t in rs.Tweets)
                    {
                        if (t.CreatedAt.Value > newest)
                        {
                            newest = t.CreatedAt.Value;
                        }
                    }

                    //Upload the newest time
                    await UploadMostRecentlyProcessedTweetProcessedAtAsync(newest);
                }
            }

            return ToReturn.ToArray();
        }
    }
}