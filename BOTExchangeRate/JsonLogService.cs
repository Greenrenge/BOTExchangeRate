using GreenUtilities.JSONConfigHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOTExchangeRate
{
    public class JsonLogService
    {
        private readonly string _path;
        private SyncLog _log;
        private int _daysToRecover;
        public JsonLogService(string path, int daysToRecover = 30)
        {
            _daysToRecover = daysToRecover;
            _path = path;
            _log = FetchLog();
        }
        /// <summary>
        /// force read the file in the path, should return null if not found.
        /// </summary>
        /// <returns></returns>
        private SyncLog FetchLog()
        {
            SyncLog log;
            JSONConfigurationManager<SyncLog> logManager = new JSONConfigurationManager<SyncLog>();
            try
            {
                log = logManager.ReadConfig(_path);
                if (log == null) log = new SyncLog();
                return log;
            }
            catch (Exception ex)
            {
                ExceptionHandling.LogException(ex);
                log = new SyncLog();
                return log;
            }


        }
        /// <summary>
        /// save the config file to the path, after done the operations.
        /// </summary>
        /// <returns>true if not error occured in try block.</returns>
        private bool SaveLog(SyncLog log)
        {
            JSONConfigurationManager<SyncLog> logManager = new JSONConfigurationManager<SyncLog>(log);
            try
            {
                logManager.WriteConfig(_path);
                return true;
            }
            catch (Exception ex)
            {
                ExceptionHandling.LogException(ex);
                return false;
            }

        }

        public List<DailyLog> GetAllLog()
        {
            return _log.Log;
        }

        public DailyLog GetDailyLog(DateTime date)
        {
            var log = _log.Log.Where(l => l.Date == date);
            if (log.Count() > 0) return log.First();
            else
            {
                var newLog = new DailyLog { Date = date, CurrenciesRate = new List<CurrencyRate>() };
                _log.Log.Add(newLog);
                return newLog;
            }
        }

        public CurrencyRate GetCurrency(DateTime date, string currency)
        {
            var dailyLog = GetDailyLog(date);
            var currencyRate = dailyLog.CurrenciesRate.Where(c => c.Currency == currency);
            if (currencyRate.Count() > 0) return currencyRate.First();
            else
            {
                var newPair = new CurrencyRate { Date = date, Currency = currency, isSyncSAP = false, isAPIComplete = false };
                dailyLog.CurrenciesRate.Add(newPair);
                return newPair;
            }
        }

        /// <summary>
        /// caller should send the same date of dailylog and record
        /// </summary>
        /// <param name="log"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        public bool InsertOrReplaceRecord(DailyLog log, CurrencyRate record)
        {
            try
            {
                if (log.CurrenciesRate == null) log.CurrenciesRate = new List<CurrencyRate>();
                var existing = log.CurrenciesRate.Where(c => c.Currency == record.Currency);
                if (!existing.Any())
                {
                    log.CurrenciesRate.Add(record);
                    record.Date = log.Date;// replace date in record 
                    return true;
                }
                else
                {
                    foreach (var rec in existing)
                    {
                        log.CurrenciesRate.Remove(rec);
                    }
                    log.CurrenciesRate.Add(record);
                    record.Date = log.Date;// replace date in record 
                    return true;
                }
            }
            catch (Exception ex)
            {
                ExceptionHandling.LogException(ex);
                return false;
            }

        }

        public bool SaveChange()
        {
            _log.ReflectForSave();
            return SaveLog(_log);
        }



        public List<CurrencyRate> GetUnfinishedBOTSync()
        {
            var output = new List<CurrencyRate>();
            if (_log != null)
            {
                foreach (var record in _log.Log)
                {
                    var unfinished = record.CurrenciesRate.Where(x => x.isAPIComplete == false);
                    if (unfinished.Count() > 0) output.AddRange(unfinished);//passed by ref
                }
            }
            return output;
        }
        public List<CurrencyRate> GetUnfinishedSAPSync()
        {
            var output = new List<CurrencyRate>();
            if (_log != null)
            {
                foreach (var record in _log.Log)
                {
                    var unfinished = record.CurrenciesRate.Where(x => x.isSyncSAP == false);
                    if (unfinished.Count() > 0) output.AddRange(unfinished);
                }
            }
            return output;
        }


        /* /// <summary>
         /// whether update BOT API or SAP call
         /// </summary>
         /// <param name="records"></param>
         /// <returns></returns>
         public bool UpdateSyncLog(List<CurrecyRate> records)
         {
             //_log = FetchLog();
             var rec = records.GroupBy(x => x.Date).Select(x => new DailyLog { Date = x.Key, CurrenciesRate = x.ToList() });
             if (_log == null)
             {
                 _log = new SyncLog();
                 _log.Log = rec.ToList();
                 _log.ReflectForSave();
                 return SaveLog(_log);
             }
             else
             {
                 foreach (var day in rec)
                 {
                     var sameDateInLog = _log.Log.Where(x => x.Date == day.Date);
                     if (sameDateInLog.Any())
                     {
                         var count = sameDateInLog.Count();
                         DailyLog log = null;
                         if (count > 1)
                         {
                             log = sameDateInLog.First();//update the first one
                             sameDateInLog = sameDateInLog.Skip(1).Take(count - 1);
                             foreach (var item in sameDateInLog)
                             {
                                 _log.Log.Remove(item);//delete dub in _log
                             }
                         }
                         else if (count == 1) log = sameDateInLog.First();

                         if (log != null)
                         {
                             //TODO : replace only the same currency not all currency
                             log.CurrenciesRate = day.CurrenciesRate;
                         }
                     }
                     else
                     {
                         _log.Log.Add(day);//if rec has dublicated record, the last entity in collection will replace others
                     }
                 }
                 _log.ReflectForSave();
                 if (_log.Count > _daysToRecover)
                 {
                     _log.Log = _log.Log.OrderByDescending(x => x.Date).Take(_daysToRecover).ToList();
                 }
                 _log.ReflectForSave();
                 return SaveLog(_log);
             }
         }*/

    }
}
