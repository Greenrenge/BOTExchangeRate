using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOTExchangeRate
{
    public class SyncLog
    {
        public DateTime LastUpdate { get; set; }
        public int Count { get; set; }
        public List<DailyLog> Log { get; set; }
    }
    public class DailyLog
    {
        public DateTime Date { get; set; }
        public List<CurrecyRate> CurrenciesRate { get; set; }
    }
    public class CurrecyRate
    {
        public DateTime Date { get; set; }
        public string Currency { get; set; }
        public decimal Buy { get; set; }
        public decimal Sell { get; set; }
        public bool isAPIComplete { get; set; }
        public bool isSyncSAP { get; set; }
    }
}
