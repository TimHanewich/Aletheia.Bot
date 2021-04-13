using System;
using Aletheia;
using Aletheia.Service.Fundamentals;
using Aletheia.Service;
using Aletheia.Fundamentals;
using QuickChart;

namespace Aletheia.Bot
{
    public class ChartingRequest
    {
        public string CompanyId {get; set;}
        public PeriodType PeriodType {get; set;}
        public FactLabel[] FactLabels {get; set;}
        public DateTime? After {get; set;}
        public DateTime? Before {get; set;}
    }
}