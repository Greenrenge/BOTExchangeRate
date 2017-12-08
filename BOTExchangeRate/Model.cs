using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOTExchangeRate
{
    public class SyncLog
    {
        public SyncLog()
        {
            Log = new List<DailyLog>();
            Count = 0;
            LastUpdate = DateTime.Now;
        }
        public DateTime LastUpdate { get; set; }
        public int Count { get; set; }
        public List<DailyLog> Log { get; set; }
        public void ReflectForSave()
        {
            if (Log != null)
                Count = Log.Count;
            else
            {
                Count = 0;
                Log = new List<DailyLog>();
            }
            Log = Log.OrderByDescending(x=>x.Date).ToList();//order by date , last updated is on the top
            LastUpdate = DateTime.Now;
        }
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
