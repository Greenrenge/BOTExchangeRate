using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaleeUtilities;
using MaleeUtilities.Extensions;
using System.Runtime.ExceptionServices;

namespace BOTExchangeRate
{
    public class Appconfig : BaseWebConfig
    {
        public static string LOG4NET_DEBUG
        {
            get
            {
                return GetFullPath("LOG4NET_DEBUG");
            }
        }
        public static string LOG4NET_ERROR
        {
            get
            {
                return GetFullPath("LOG4NET_ERROR");
            }
        }
        public static string JsonLog
        {
            get
            {
                return GetFullPath("JsonLog");
            }
        }

        public static List<string> SyncCurrency
        {
            get
            {
                return GetValue("SyncCurrency", typeof(List<string>), ',') as List<string>;
            }
        }
        public static List<string> CurrencyRatio
        {
            get
            {
                return GetValue("CurrencyRatio", typeof(List<string>), ',') as List<string>;
            }
        }
        public static Dictionary<string,int> CurrencyRatioDict
        {
            get
            {
                Dictionary<string, int> output = new Dictionary<string, int>();
                var currencyRatio = CurrencyRatio;
                foreach(var setting in currencyRatio)
                {
                    var currency = setting.SplitAndSelectAtIndex('=', 0);//setting.SubstringUntil("=");
                    int ratio = Convert.ToInt32(setting.SplitAndSelectAtIndex('=', 1));
                    if (!output.ContainsKey(currency)) output.Add(currency, ratio);
                }
                return output;
            }
        }

        public static string BOTAPIKey
        {
            get
            {
                return GetString("BOTAPIKey");
            }
        }
        public static string BOTServiceEndPoint
        {
            get
            {
                return GetString("BOTServiceEndPoint");
            }
        }
        public static int BOTHourUpdate
        {
            get
            {
                return GetInteger("BOTHourUpdate");
            }
        }
        public static string BuyValue
        {
            get
            {
                return GetString("BuyValue");
            }
        }
        public static string SellValue
        {
            get
            {
                return GetString("SellValue");
            }
        }
        public static bool RecoveryMode
        {
            get
            {
                return (bool)GetValue("RecoveryMode", typeof(bool));
            }
        }
        public static List<string> RecoveryDate
        {
            get
            {
                return GetValue("RecoveryDate", typeof(List<string>), ',') as List<string>;
            }
        }
        public static string SAPServerHost
        {
            get
            {
                return GetString("SAPServerHost");
            }
        }
        public static string SAPSystemNumber
        {
            get
            {
                return GetString("SAPSystemNumber");
            }
        }
        public static string SAPSystemID
        {
            get
            {
                return GetString("SAPSystemID");
            }
        }
        public static string SAPUser
        {
            get
            {
                return GetString("SAPUser");
            }
        }
        public static string SAPPassword
        {
            get
            {
                return GetString("SAPPassword");
            }
        }
        public static string SAPClient
        {
            get
            {
                return GetString("SAPClient");
            }
        }
    }

    internal static class ExceptionHandling
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ExceptionHandling));
        public static void ThrowException(Exception ex, string Identifier = "")
        {
            try
            {
                log.Error(String.Format(@" EXCEPTION OCCURED ON [{0}] ", Identifier), ex);
                var captured = ExceptionDispatchInfo.Capture(ex);
                if (captured != null)
                {
                    captured.Throw();    //call stack from MyMethod will be kept
                }
            }
            catch
            {

            }
        }
        public static void LogException(Exception ex, string Identifier = "")
        {
            try
            {
                log.Error(String.Format(@" EXCEPTION OCCURED ON [{0}] ", Identifier), ex);
            }
            catch
            {

            }
        }
    }
}
