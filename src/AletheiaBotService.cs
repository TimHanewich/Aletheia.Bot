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

namespace Aletheia.Bot
{
    public class AletheiaBotService
    {
        private string apikey;

        public AletheiaBotService(string api_key)
        {
            apikey = api_key;
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
    
        public string AssembleQuickChartRequestUrlFromData(KeyValuePair<FactLabel, FinancialFactTrendDataPoint[]>[] data_points)
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
            qc.Width = 500;
            qc.Height = 300;
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
    }
}