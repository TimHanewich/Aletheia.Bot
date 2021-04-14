using System;
using Aletheia;
using Aletheia.Service.Fundamentals;
using Aletheia.Service;
using Aletheia.Fundamentals;
using QuickChart;
using System.Collections.Generic;

namespace Aletheia.Bot
{
    public class ChartingRequest
    {
        public string CompanyId {get; set;}
        public PeriodType PeriodType {get; set;}
        public FactLabel[] FactLabels {get; set;}
        public DateTime? After {get; set;}
        public DateTime? Before {get; set;}

        public static ChartingRequest ParseFromStandardRequest(string request)
        {
            //Example:
            // chart MSFT annual
            // revenue
            // income
            // investing cash flow
            // after 12/31/2017
            // before 12/31/2020

            ChartingRequest ToReturn = new ChartingRequest();

            List<string> Splitter = new List<string>();
            Splitter.Add(Environment.NewLine);
            Splitter.Add("\n");
            
            //Go through each line and parse out meaning
            string[] lines = request.Split(Splitter.ToArray(), StringSplitOptions.RemoveEmptyEntries);
            List<FactLabel> RequestedFactLabels = new List<FactLabel>();
            foreach (string line in lines)
            {

                //Charting line
                if (line.ToLower().Contains("chart"))
                {
                    Splitter.Clear();
                    Splitter.Add(" ");
                    string[] parts = line.Split(Splitter.ToArray(), StringSplitOptions.RemoveEmptyEntries);

                    //Chart command
                    if (parts.Length != 3)
                    {
                        throw new Exception("Chart command line follows incorrect syntax.");
                    }
                    if (parts[0].ToLower() != "chart")
                    {
                        throw new Exception("First word of command is not 'chart'. Command not understood.");
                    }

                    //Trading symbol
                    ToReturn.CompanyId = parts[1];
                    ToReturn.CompanyId = ToReturn.CompanyId.Replace("$", ""); //Remove dollar symbol

                    //Annual or quarterly?
                    if (parts[2].ToLower() == "annual")
                    {
                        ToReturn.PeriodType = PeriodType.Annual;
                    }
                    else if (parts[2].ToLower() == "quarterly")
                    {
                        ToReturn.PeriodType = PeriodType.Quarterly;
                    }
                    else
                    {
                        throw new Exception("Period type '" + parts[2] + " not understood.");
                    }
                }
                else if (line.ToLower().Contains("after")) //After param
                {
                    Splitter.Clear();
                    Splitter.Add(" ");
                    string[] parts = line.Split(Splitter.ToArray(), StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        ToReturn.After = DateTime.Parse(parts[1]);
                    }
                    catch
                    {
                        throw new Exception("Unable to parse date '" + parts[1] + "'");
                    }
                }
                else if (line.ToLower().Contains("before")) //Before param
                {
                    Splitter.Clear();
                    Splitter.Add(" ");
                    string[] parts = line.Split(Splitter.ToArray(), StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        ToReturn.Before = DateTime.Parse(parts[1]);
                    }
                    catch
                    {
                        throw new Exception("Unable to parse date '" + parts[1] + "'");
                    }
                }
                else //it must be a fact label request (somewhere in the middle of the requst. For example, revenue)
                {
                    List<KeyValuePair<string, FactLabel>> cmdFlPairs = new List<KeyValuePair<string, FactLabel>>();
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("revenue", FactLabel.Revenue));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("sg&a", FactLabel.SellingGeneralAndAdministrativeExpense));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("r&d", FactLabel.ResearchAndDevelopmentExpense));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("operating income", FactLabel.OperatingIncome));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("net income", FactLabel.NetIncome));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("assets", FactLabel.Assets));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("liabilities", FactLabel.Liabilities));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("equity", FactLabel.Equity));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("cash", FactLabel.Cash));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("current assets", FactLabel.CurrentAssets));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("current liabilities", FactLabel.CurrentLiabilities));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("retained earnings", FactLabel.RetainedEarnings));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("common shares", FactLabel.CommonStockSharesOutstanding));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("operating cf", FactLabel.OperatingIncome));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("investing cf", FactLabel.InvestingCashFlows));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("financing cf", FactLabel.FinancingCashFlows));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("debt issuance", FactLabel.ProceedsFromIssuanceOfDebt));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("debt payment", FactLabel.PaymentsOfDebt));
                    cmdFlPairs.Add(new KeyValuePair<string, FactLabel>("dividends paid", FactLabel.DividendsPaid));

                    foreach (KeyValuePair<string, FactLabel> kvp in cmdFlPairs)
                    {
                        if (line.ToLower() == kvp.Key)
                        {
                            RequestedFactLabels.Add(kvp.Value);
                        }
                    }
                }

            

            }

            //Plug in the fact labels
            ToReturn.FactLabels = RequestedFactLabels.ToArray();

            //Check to make sure they are all there
            if (ToReturn.CompanyId == null)
            {
                throw new Exception("Company ID not specified or collected.");
            }
            if (ToReturn.FactLabels == null || ToReturn.FactLabels.Length == 0)
            {
                throw new Exception("You must specify at least one fact label.");
            }

            return ToReturn;
        }
    }
}