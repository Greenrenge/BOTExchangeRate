using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaleeUtilities;
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

        //public static List<string> TableToSync
        //{
        //    get
        //    {
        //        return GetValue("TableToSync", typeof(List<string>), ',') as List<string>;
        //    }
        //}
        //public static List<string> TableOptions
        //{
        //    get
        //    {
        //        return GetValue("TableOptions", typeof(List<string>), ',') as List<string>;
        //    }
        //}
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
