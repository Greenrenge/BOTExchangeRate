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
        public JsonLogService(string path,int daysToRecover=30)
        {
            _daysToRecover = daysToRecover;
            _path = path;
            //_log = FetchLog();
        }
        /// <summary>
        /// force read the file in the path, should return null if not found.
        /// </summary>
        /// <returns></returns>
        private SyncLog FetchLog()
        {
            JSONConfigurationManager<SyncLog> logManager = new JSONConfigurationManager<SyncLog>();
            return logManager.ReadConfig(_path);
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

        public List<CurrecyRate> GetUnfinishedBOTSync()
        {
            var output = new List<CurrecyRate>();
            _log = FetchLog();
            if (_log != null)
            {
                foreach (var record in _log.Log)
                {
                    var unfinished = record.CurrenciesRate.Where(x => x.isAPIComplete == false);
                    output.AddRange(unfinished);
                }
            }
            return output;
        }
        public List<CurrecyRate> GetUnfinishedSAPSync()
        {
            var output = new List<CurrecyRate>();
            _log = FetchLog();
            if (_log != null)
            {
                foreach (var record in _log.Log)
                {
                    var unfinished = record.CurrenciesRate.Where(x => x.isSyncSAP == false);
                    output.AddRange(unfinished);
                }
            }
            return output;
        }

        /// <summary>
        /// whether update BOT API or SAP call
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public bool UpdateSyncLog(List<CurrecyRate> records)
        {
            _log = FetchLog();
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
                    if(sameDateInLog.Any())
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

                        if( log !=null)
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
                if(_log.Count > _daysToRecover)
                {
                    _log.Log = _log.Log.OrderByDescending(x => x.Date).Take(_daysToRecover).ToList();
                }
                _log.ReflectForSave();
                return SaveLog(_log);
            }
        }

    }
}
