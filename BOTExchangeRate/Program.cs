using log4net;
using MaleeUtilities.Mail;
using MaleeUtilities.SAP.Utils;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOTExchangeRate
{
    //write log that programs is run
    //check json to get last date that is not complete (on any currency)
    //call api to get data
    //convert the data to object and keep in json log (also delete older than 30 days out of log)
    //call sap (check the log to write only incompleted record) and if success update json log
    //CallRESTAysnc().Wait();
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger("Main");
        static void Main(string[] args)
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Appconfig.Initialize(path, ConfigurationManager.AppSettings, null);

            #region Logger
            log4net.GlobalContext.Properties["LOG4NET_ERROR"] = Appconfig.LOG4NET_ERROR; //log file path
            log4net.GlobalContext.Properties["LOG4NET_DEBUG"] = Appconfig.LOG4NET_DEBUG; //log file path
            log4net.Config.XmlConfigurator.Configure();
            #endregion
            var currencyRatioDict = Appconfig.CurrencyRatioDict;
            DateTime programDatetime = DateTime.Now;
            DateTime runningDate;
            if (programDatetime.Hour > Appconfig.BOTHourUpdate)
            {
                runningDate = new DateTime(programDatetime.Year, programDatetime.Month, programDatetime.Day);
            }
            else
            {
                var programDatetimeYesterday = programDatetime.AddDays(-1);
                runningDate = new DateTime(programDatetimeYesterday.Year, programDatetimeYesterday.Month, programDatetimeYesterday.Day);
            }
            if (args != null && args.Count() > 0)
            {
                DateTime tryResult;
                if (DateTime.TryParseExact(args.First(), "d/MM/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out tryResult))
                {
                    runningDate = tryResult;
                }
            }

            log.Debug("*******************************************************************");
            log.Debug("PROGRAM RUNS ON " + programDatetime.ToString("dd/MM/yyyy HH:mm:ss"));
            log.Debug("*******************************************************************");
            JsonLogService db = new JsonLogService(Appconfig.JsonLog);
            var dateToRun = new List<DateTime>();
            if (!Appconfig.RecoveryMode)
            {
                dateToRun.Add(runningDate);// we could check last date run is yesterday|| today ? if not we cound add lost day in between
                var lastDataDate = db.GetLastRunningDate();
                if(lastDataDate != default(DateTime) && lastDataDate.CompareTo(runningDate) <0)// 0 means run second time for the day, 1 is not possible (if we do not force them to run future's day)
                {
                    log.Debug("-----------------------------------------------------------------");
                    log.Debug("CHECKING LAST RUNING DATE FOR SPECIFY DATES TO RUN (IF THERE IS MISSING RUNNING ON SOME DAYS)");
                    log.Debug("-----------------------------------------------------------------");
                    var timeSpanDiff = runningDate.Subtract(lastDataDate);
                    if(timeSpanDiff.TotalDays > 1)
                    {
                        int dayToRecover = Convert.ToInt32(timeSpanDiff.TotalDays) - 1;
                        for(int i = 1; i <= dayToRecover; i++)
                        {
                            dateToRun.Add(runningDate.AddDays(-i));
                        }
                    }
                }
            }
            else
            {
                foreach (var recoveryDate in Appconfig.RecoveryDate)
                {
                    DateTime tryResult;
                    if (DateTime.TryParseExact(recoveryDate, "d/M/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out tryResult))
                    {
                        dateToRun.Add(tryResult);
                    }
                }
            }

            foreach (var transactionDate in dateToRun)
            {

                #region  BOT API 
                log.Debug("-----------------------------------------------------------------");
                log.Debug("API Call Start");
                log.Debug("-----------------------------------------------------------------");

                //var todayLog = db.GetDailyLog(transactionDate);//??
                List<Task> taskList = new List<Task>();
                foreach (var currencyPair in Appconfig.SyncCurrency)
                {
                    var currencyInDb = db.GetOrCreateCurrency(transactionDate, currencyPair);
                    //if sap is sync we will not call api again
                    if (currencyInDb.isSyncSAP == false)
                    {
                        if (currencyInDb.isAPIComplete)
                        {
                            log.Debug(String.Format(@"NOTE : THERE WAS FINISHED API CALL BUT SAP ERROR --> RECALL API AGAIN {0} {1}",currencyInDb.Currency,currencyInDb.Date.ToShortDateString()));
                        }
                        taskList.Add(APIExecute(currencyInDb, transactionDate));
                    }
                }

                var unfinishedAPI = db.GetUnfinishedBOTSync().Where(x => x.Date != transactionDate && Appconfig.SyncCurrency.Contains(x.Currency) && x.isSyncSAP == false);
                foreach (var currencyPair in unfinishedAPI)
                {
                    taskList.Add(APIExecute(currencyPair, currencyPair.Date));
                }
                Task.WhenAll(taskList).Wait();
                log.Debug("-----------------------------------------------------------------");
                log.Debug("API Call Successfully");
                log.Debug("-----------------------------------------------------------------");
                if (!db.SaveChange())
                {
                    log.Debug("-----------------------------------------------------------------");
                    log.Debug("Log Update Error");
                    log.Debug("-----------------------------------------------------------------");
                }
                else
                {
                    log.Debug("-----------------------------------------------------------------");
                    log.Debug("Log Update Successful");
                    log.Debug("-----------------------------------------------------------------");
                }

                #endregion

            }

            #region API Patch for non-value date
            log.Debug("==============================================================================");
            log.Debug("CHECK PATCHING START");
            log.Debug("==============================================================================");
            try
            {
                var patchingNeededDates = db.GetAllLog().Where(x => x.CurrenciesRate.Any(c => Appconfig.SyncCurrency.Contains(c.Currency) && c.isAPIComplete == true && c.isSyncSAP == false && c.Sell_SAP == (decimal)0 && c.Buy_SAP == (decimal)0)).ToList();//REVIEW
                if (patchingNeededDates.Count > 0)
                {
                    log.Debug("PATCHING FOUND");
                    Patching(Appconfig.SyncCurrency, patchingNeededDates);
                    log.Debug("PATCHING END");
                }
            }
            catch (Exception ex )
            {
                log.Debug("==============================================================================");
                log.Debug("CHECK PATCHING ERROR : CHECK THE EXCEPTION BELOW");
                log.Debug("==============================================================================");
                ExceptionHandling.LogException(ex);
            }
           
            #endregion

            #region SAP Part
            /// we will send sap only isAPIcomplete and if none of Buy and Sell we will use the last known value before that day to be value sent to SAP
            /// send only currency config, only isAPIComplete = true, only isSAPComplete = false
            log.Debug("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            log.Debug("SAP START");
            log.Debug("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            bool isSAPCompleted = true;
            string SAPErrorMsg = string.Empty;
            try
            {
                var sapSent = db.GetAllLog().Where(x => x.CurrenciesRate.Any(c => Appconfig.SyncCurrency.Contains(c.Currency) && c.isAPIComplete == true && c.isSyncSAP == false && c.Sell_SAP != (decimal)0 && c.Buy_SAP != (decimal)0)).ToList();
                #region  SAP Connection
                DestinationRegister.RegistrationDestination(new SAPDestinationSetting
                {
                    AppServerHost = Appconfig.SAPServerHost,
                    Client = Appconfig.SAPClient,
                    User = Appconfig.SAPUser,
                    Password = Appconfig.SAPPassword,
                    SystemNumber = Appconfig.SAPSystemNumber,
                    SystemID = Appconfig.SAPSystemID,
                });
                var des = RfcDestinationManager.GetDestination(DestinationRegister.Destination());
                IRfcFunction function = des.Repository.CreateFunction("ZBAPI_EXCHANGERATE_UPDATE");
                #endregion

                #region example for input structure as input bapi

                /*
                 * TABLE :I_EXCHANGE
                 STRUCTURE BAPI1093_0
                    RATE_TYPE	Exchange Rate Type , B = buy // M = sell
                    FROM_CURR	From currency
                    TO_CURRNCY	To-currency
                    VALID_FROM	Date from Which Entry Is Valid (yyyy-MM-dd)
                    EXCH_RATE	Direct Quoted Exchange Rate
                    FROM_FACTOR	Ratio for the "From" Currency Units, 1 // if JPY this is 100
                    TO_FACTOR	Ratio for the "To" Currency Units, 1
                    EXCH_RATE_V	Indirect Quoted Exchange Rate ****No input
                    FROM_FACTOR_V	Ratio for the "From" Currency Units ****No input
                    TO_FACTOR_V	Ratio for the "To" Currency Units ****No input
                */
                IRfcTable table = function["I_EXCHANGE"].GetTable();//table
                List<CurrencyRate> sentSAP = new List<CurrencyRate>();
                foreach (var dailyLog in sapSent)
                {
                    foreach (var cur in dailyLog.CurrenciesRate.Where(c => Appconfig.SyncCurrency.Contains(c.Currency) && c.isAPIComplete == true && c.isSyncSAP == false && c.Sell_SAP != (decimal)0 && c.Buy_SAP != (decimal)0))
                    {
                        table.Append();//create new row
                        IRfcStructure Buy = table.CurrentRow;//current structure ,row
                        string structure_name = Buy.Metadata.Name;
                        //Buy
                        Buy.SetValue("RATE_TYPE", "B");
                        Buy.SetValue("FROM_CURR", cur.Currency);
                        Buy.SetValue("TO_CURRNCY", "THB");
                        Buy.SetValue("VALID_FROM", dailyLog.Date.ToString("yyyy-MM-dd", new CultureInfo("en-US")));
                        Buy.SetValue("EXCH_RATE", cur.Buy_SAP.ToString("0.#####"));
                        if (currencyRatioDict.ContainsKey(cur.Currency)) Buy.SetValue("FROM_FACTOR", currencyRatioDict[cur.Currency]);
                        else Buy.SetValue("FROM_FACTOR", 1);
                        Buy.SetValue("TO_FACTOR", 1);
                        log.Debug(String.Format("{0}  {1}  {2}  {3}  {4}", "B", cur.Currency, "THB", dailyLog.Date.ToString("ddMMyyyy", new CultureInfo("en-US")), cur.Buy_SAP.ToString("0.#####")));

                        table.Append();//create new row
                        IRfcStructure Sell = table.CurrentRow;//current structure ,row
                                                              //Sell
                        Sell.SetValue("RATE_TYPE", "M");
                        Sell.SetValue("FROM_CURR", cur.Currency);
                        Sell.SetValue("TO_CURRNCY", "THB");
                        Sell.SetValue("VALID_FROM", dailyLog.Date.ToString("yyyy-MM-dd", new CultureInfo("en-US")));
                        Sell.SetValue("EXCH_RATE", cur.Sell_SAP.ToString("0.#####"));
                        if (currencyRatioDict.ContainsKey(cur.Currency)) Sell.SetValue("FROM_FACTOR", currencyRatioDict[cur.Currency]);
                        else Sell.SetValue("FROM_FACTOR", 1);
                        Sell.SetValue("TO_FACTOR", 1);
                        log.Debug(String.Format("{0}  {1}  {2}  {3}  {4}", "M", cur.Currency, "THB", dailyLog.Date.ToString("ddMMyyyy", new CultureInfo("en-US")), cur.Sell_SAP.ToString("0.#####")));
                        sentSAP.Add(cur);
                    }
                }

                var count = table.Count;
                #endregion
                if (count > 0)
                {
                    try
                    {

                        function.Invoke(des);
                        sentSAP.ForEach(x =>
                        {
                            x.isSyncSAP = true;
                        });

                    }
                    catch (SAP.Middleware.Connector.RfcAbapClassicException ex)
                    {
                        if (ex.Key == "SAPSQL_ARRAY_INSERT_DUPREC")
                        {
                            //dublicate record found
                            log.Debug("-----------------------------------------------------------------");
                            log.Debug("SAP BAPI CALL Error : DUBLICATED RECORD FOUND IN SAP.");
                            log.Debug("-----------------------------------------------------------------");
                        }
                        ExceptionHandling.LogException(ex);
                    }
                    catch (Exception ex)
                    {
                        isSAPCompleted = false;
                        SAPErrorMsg += Environment.NewLine + ex.Message;
                        log.Debug("-----------------------------------------------------------------");
                        log.Debug("SAP BAPI CALL Error : READ THE EXCEPTION DETAIL.");
                        log.Debug("-----------------------------------------------------------------");
                        ExceptionHandling.LogException(ex);
                    }
                }
                //Call bapi

                #region example for fetch structure as object
                /*
                 *  IRfcParameter export = function["PRHEADER"];
                IRfcStructure structure = export.GetStructure();
                var setting = new PropertiesList<DataContainer>
                {
                    { "PREQ_NO", x=>x.PREQ_NO},
                    { "PREQ_NO", x=>x.PREQ_NO},
                    { "PR_TYPE", x=>x.PR_TYPE},
                    { "CTRL_IND", x=>x.CTRL_IND},
                };
                DataContainer output = structure.ToObject(setting);*/
                #endregion

                IRfcParameter returnTable = function["I_EXCHANGE"];
                IRfcTable table1 = returnTable.GetTable();

                //foreach (IRfcStructure record in table1)
                //{
                //    Console.WriteLine(String.Format("{0}:{1}", record.GetInt("PREQ_ITEM"), record.GetValue("PREQ_ITEM").ToString()));
                //}

                if (!db.SaveChange())
                {
                    log.Debug("-----------------------------------------------------------------");
                    log.Debug("Log Update Error");
                    log.Debug("-----------------------------------------------------------------");
                }
                else
                {
                    log.Debug("-----------------------------------------------------------------");
                    log.Debug("Log Update Successful");
                    log.Debug("-----------------------------------------------------------------");
                }
            }
            catch (Exception ex)
            {
                isSAPCompleted = false;
                SAPErrorMsg += Environment.NewLine + ex.Message;
                log.Debug("-----------------------------------------------------------------");
                log.Debug("SAP PART Error :  READ THE EXCEPTION DETAIL.");
                log.Debug("-----------------------------------------------------------------");
                ExceptionHandling.LogException(ex);
            }

            #endregion



            #region AlertMsg

            try
            {
                if (!Appconfig.RecoveryMode
               && db.GetOrCreateDailyLog(runningDate).CurrenciesRate.Any(x => Appconfig.SyncCurrency.Contains(x.Currency) && x.isSyncSAP == false && x.Sell != (decimal)0 && x.Buy != (decimal)0)
               && programDatetime.Date.AddDays(-1) == runningDate.Date
               && programDatetime.Hour < Appconfig.BOTHourUpdate && programDatetime.Hour > Appconfig.AlertCutOffTime)
                {
                    //recovery mode is off
                    //there is some sync currency which cannot sent to SAP found and it is not dayoff (sell && buy !=0)
                    //program is running on not today for sure (not after 18.00 of the day) (not before 7.00 of the program date)

                    MailService mailService = new MailService(Appconfig.MailServer, Appconfig.MailServerPort);

                    //send email to user
                    Email userMail = new Email();
                    userMail.Cc = Appconfig.AlertAdminEmail;
                    userMail.From = Appconfig.MailAdminAddress;
                    userMail.To = Appconfig.AlertUserEmail;
                    userMail.Subject = @"[BOTExchangeRate] ERROR UPDATE TO SAP NOTIFICATION";
                    userMail.SenderName = @"BOTExchange Automatic Email Alert";
                    userMail.BodyMessage = MailTemplateUser(runningDate, Appconfig.SyncCurrency);

                    mailService.SendMail(userMail);
                    //send email to admin if exception found
                    if (!isSAPCompleted)
                    {
                        Email adminMail = new Email();
                        adminMail.From = Appconfig.MailAdminAddress;
                        adminMail.To = Appconfig.AlertAdminEmail;
                        adminMail.Subject = @"[BOTExchangeRate] ERROR UPDATE TO SAP NOTIFICATION [DEBUG]";
                        adminMail.SenderName = @"BOTExchange Automatic Email Alert";
                        adminMail.BodyMessage = MailTemplateAdmin(runningDate, Appconfig.SyncCurrency, SAPErrorMsg);
                        mailService.SendMail(adminMail);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug("-----------------------------------------------------------------");
                log.Debug("EMAIL ALERT ERROR");
                log.Debug("-----------------------------------------------------------------");
                ExceptionHandling.LogException(ex);
            }
           
            #endregion


            log.Debug("*******************************************************************");
            log.Debug("PROGRAM RUNS COMPLETED " + programDatetime.ToString("dd/MM/yyyy HH:mm:ss"));
            log.Debug("*******************************************************************");
        }

        private static string MailTemplateUser(DateTime date,List<string> currencies)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div style='font:16px calibri,sans-serif;'>This email is to notify you that,<br/>" +
                      "There was <span style='color:red'> error </span> found in BOT Exchange Rate Auto Update To SAP.<br/>");
            sb.Append(@"<b>Please consider to MANUAL INPUT FOLLOWING DATA IN SAP</b>");
            sb.AppendFormat(@"
                                        <div style='margin: 10px 0 10px 30px;font:14px calibri,sans-serif;'>
                                             Exchange Rate Date : {0} <br/>
                                             Currencies : {1} <br/>
                                        </div>
                                        <br/>                      
                                        
                                        <span style='font-size:x-small'>Please do not reply this email.</span>
                                        </div>",
                                        date.ToString("dd/MM/yyyy"),String.Join(",",currencies)
                                        );
            return sb.ToString();
        }
        private static string MailTemplateAdmin(DateTime date, List<string> currencies,string errorMsg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div style='font:16px calibri,sans-serif;'>This email is to notify you that,<br/>" +
                      "There was <span style='color:red'> error </span> found in BOT Exchange Rate Auto Update To SAP.<br/>");
            sb.Append(@"<b>Please consider to MANUAL INPUT FOLLOWING DATA IN SAP</b>");
            sb.AppendFormat(@"
                                        <div style='margin: 10px 0 10px 30px;font:14px calibri,sans-serif;'>
                                             Exchange Rate Date : {0} <br/>
                                             Currencies : {1} <br/>
                                             Error Detail : {2} <br/>
                                        </div>
                                        <br/>                      
                                        
                                        <span style='font-size:x-small'>Please do not reply this email.</span>
                                        </div>",
                                        date.ToString("dd/MM/yyyy"), String.Join(",", currencies), errorMsg
                                        );
            return sb.ToString();
        }
        private static async Task APIExecute(CurrencyRate db, DateTime transactionDate)
        {
            var resultRest = await BOTBusinessService.GetRate(db.Currency, transactionDate);
            if (resultRest.Success == false)
            {
                db.isAPIComplete = false;

            }
            else
            {
                db.Date = transactionDate;
                db.isAPIComplete = resultRest.Data.isAPIComplete;
                db.Buy = resultRest.Data.Buy;
                db.Sell = resultRest.Data.Sell;
                db.Buy_SAP = db.Buy;
                db.Sell_SAP = db.Sell;
            }

        }

        public static void Patching(List<string> currencies, List<DailyLog> log)
        {
            var allDates = log.Select(x => x.Date).ToList();
            DateTime lowerBoundary = allDates.OrderBy(x => x).First().AddDays(-10);//6/1/2018
            DateTime higherBoundary = allDates.OrderByDescending(x => x).First();
            List<Task<List<DailyLog>>> taskList = new List<Task<List<DailyLog>>>();
            foreach (var currency in currencies)
            {
                taskList.Add(APIExecutePatching(currency, lowerBoundary, higherBoundary));
            }
            var task = Task.WhenAll(taskList);
            task.Wait();
            var result = task.Result;
            List<DailyLog> logs = new List<DailyLog>();
            foreach (var date in result)
            {
                logs = logs.Concat(date).ToList();
            }

            List<DailyLog> merged = new List<DailyLog>();
            var resultGroup = logs.GroupBy(x => x.Date);
            foreach (var group in resultGroup)
            {
                if (group.Key != default(DateTime))// we do not need error currency pair, we think that service should return empty list if currency is error.
                {
                    DailyLog groupLog = new DailyLog();
                    groupLog.Date = group.Key;
                    groupLog.CurrenciesRate = group.SelectMany(x => x.CurrenciesRate).ToList();
                    merged.Add(groupLog);
                }
            }

            //TODO : after we merge we need to specify algorithm to patch date to Buy_SAP and Sell_SAP (dont forget to error currency pair)
            merged = merged.OrderBy(x => x.Date).ToList();
            for (int i = 0; i < merged.Count; i++)
            {
                DateTime dateLower = DateTime.MinValue;
                DateTime dateHigher = DateTime.MaxValue;
                if (i == merged.Count - 1)
                {
                    dateLower = merged[i].Date;
                }
                else
                {
                    dateLower = merged[i].Date;
                    dateHigher = merged[i + 1].Date;
                }
                var patchedLog = log.Where(l => l.Date.Date.CompareTo(dateLower.Date) >= 0 && l.Date.Date.CompareTo(dateHigher.Date) < 0);
                foreach (var daylog in patchedLog)
                {
                    foreach (var currency in merged[i].CurrenciesRate)
                    {
                        var currencyToPatch = daylog.CurrenciesRate.FirstOrDefault(x => x.Currency == currency.Currency);//replace unknown currency
                        if (currencyToPatch != null)
                        {
                            currencyToPatch.Buy_SAP = currency.Buy;
                            currencyToPatch.Sell_SAP = currency.Sell;
                            currencyToPatch.isAPIComplete = true;
                            currencyToPatch.isSyncSAP = false;
                        }
                        else //new currency needed when patching
                        {
                            daylog.CurrenciesRate.Add(new CurrencyRate
                            {
                                Buy = 0,
                                Sell = 0,
                                Buy_SAP = currency.Buy,
                                Sell_SAP = currency.Sell,
                                Currency = currency.Currency,
                                isAPIComplete = true,
                                isSyncSAP = false,
                                Date = daylog.Date

                            });
                        }
                    }
                }

            }
        }
        private static async Task<List<DailyLog>> APIExecutePatching(string currency, DateTime startDate, DateTime endDate)
        {
            //async call
            var resultRest = await BOTBusinessService.GetRatePatching(currency, startDate, endDate);
            if (resultRest.Success == true)
            {
                return resultRest.Data;
            }
            else
            {
                return new List<DailyLog>();//key will be default(DateTime)
                                            //error for particular currency
            }
        }
    }
}
