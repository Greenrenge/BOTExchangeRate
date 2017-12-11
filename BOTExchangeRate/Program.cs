using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BOTExchangeRate
{
    //write log that programs is run
    //check json to get last date that is not complete (on any currency)
    //call api to get data
    //convert the data to object and keep in json log (also delete older than 30 days out of log)
    //call sap (check the log to write only incompleted record) and if success update json log
    //CallRESTAysnc().Wait();
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger("Main");
        static void Main(string[] args)
        {
            DateTime programDatetime = DateTime.Now;
            DateTime transactionDate = new DateTime(programDatetime.Year, programDatetime.Month, programDatetime.Day);
            if (args != null && args.Count() > 0)
            {
                DateTime tryResult;
                if(DateTime.TryParseExact(args.First(),"dd/MM/yyyy",new CultureInfo("en-US"),DateTimeStyles.None,out tryResult))
                {
                    transactionDate = tryResult;
                }
            }
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Appconfig.Initialize(path, ConfigurationManager.AppSettings, null);

            #region Logger
            log4net.GlobalContext.Properties["LOG4NET_ERROR"] = Appconfig.LOG4NET_ERROR; //log file path
            log4net.GlobalContext.Properties["LOG4NET_DEBUG"] = Appconfig.LOG4NET_DEBUG; //log file path
            log4net.Config.XmlConfigurator.Configure();
            #endregion


            log.Debug("-----------------------------------------------------------------");
            log.Debug("PROGRAM RUNS ON " + programDatetime.ToString("dd/MM/yyyy"));
            log.Debug("-----------------------------------------------------------------");


            log.Debug("-----------------------------------------------------------------");
            log.Debug("API Call Start");
            log.Debug("-----------------------------------------------------------------");
            JsonLogService db = new JsonLogService(Appconfig.JsonLog);
            var todayLog = db.GetDailyLog(transactionDate);
            List<Task> taskList = new List<Task>();
            foreach (var currencyPair in Appconfig.SyncCurrency)
            {
                var currencyInDb = db.GetCurrency(transactionDate, currencyPair);
                taskList.Add(APIExecute(currencyInDb, transactionDate));
            }

            var unfinishedAPI = db.GetUnfinishedBOTSync();
            foreach (var currencyPair in unfinishedAPI)
            {
                taskList.Add(APIExecute(currencyPair, transactionDate));
            }
            Task.WhenAll(taskList).Wait();
            log.Debug("-----------------------------------------------------------------");
            log.Debug("API Call Successfully");
            log.Debug("-----------------------------------------------------------------");

            //due to we passed object by ref so it should be okay to save json in db.
            //log.Debug("-----------------------------------------------------------------");
            //log.Debug("SAP Call Start");
            //log.Debug("-----------------------------------------------------------------");
            //
            //foreach (var currencyPair in Appconfig.SyncCurrency)
            //{
            //    var currencyInDb = db.GetCurrency(transactionDate, currencyPair);
            //    taskList.Add(APIExecute(currencyInDb, transactionDate));
            //}
            //
            //foreach (var currencyPair in unfinishedAPI)
            //{
            //    taskList.Add(APIExecute(currencyPair, transactionDate));
            //}
            //Task.WhenAll(taskList).Wait();
            //log.Debug("-----------------------------------------------------------------");
            //log.Debug("SAP Call Successfully");
            //log.Debug("-----------------------------------------------------------------");
        }

        private static async Task APIExecute(CurrencyRate db,DateTime transactionDate)
        {
            var resultRest = await BOTBusinessService.GetRate(db.Currency, transactionDate);
            if(resultRest.Success == false)
            {
                db.isAPIComplete = false;

            }
            else
            {
                db.isAPIComplete = true;
                db.Buy = resultRest.Data.Buy;
                db.Sell = resultRest.Data.Sell;
            }

        }
    }
}
