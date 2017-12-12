using log4net;
using MaleeUtilities.ServiceUltil;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    public class RESTServiceCall
    {
        public static readonly ILog log = LogManager.GetLogger("RESTServiceCall");
        public static async Task<JObject> GetJSONAsync(string endpoint, NameValueCollection queryString, NameValueCollection header, int port = -1)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //https://stackoverflow.com/questions/33634605/not-receiving-response-after-postasync
                //https://developer.salesforce.com/page/Working_with_Custom_SOAP_and_REST_Services_in_.NET_Applications
                using (HttpClient client = new HttpClient())
                {
                    //the line below enables TLS1.1 and TLS1.2 (Saleforce reject TLS1.0 which used in dot net framework 4.5.2)
                    //defined remote access app - develop --> remote access --> new

                    var builder = new UriBuilder(endpoint);
                    builder.Port = port;
                    var query = HttpUtility.ParseQueryString(builder.Query);
                    foreach (var key in queryString.AllKeys)
                        query[key] = queryString[key];
                    builder.Query = query.ToString();
                    string url = builder.ToString();

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    foreach (var key in header.AllKeys)
                        request.Headers.Add(key, header[key]);

                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    //request.Content = content;
                    HttpResponseMessage response = await client.SendAsync(request);

                    string responseString = await response.Content.ReadAsStringAsync();
                    //log.Debug("Raw REST : " + responseString);
                    return JObject.Parse(responseString);
                }
            }
            catch (Exception ex)
            {
                ExceptionHandling.LogException(ex);
                return JObject.Parse(@" {'result':{'success':'false','error':{'message':'BOT Runtime Error in RESTServiceCall.GetJSONAsync() method.'}}}");
            }

        }
    }

    public class BOTBusinessService
    {
        private static readonly ILog log = LogManager.GetLogger("BOTBusinessService");
        public static async Task<Result<CurrencyRate>> GetRate(string currency, DateTime date)
        {
            log.Debug("GetRate is run for " + currency + " " + date.ToString("dd/MM/yyyy"));
            var query = new NameValueCollection();
            query["start_period"] = date.ToString("yyyy-MM-dd", new CultureInfo("en-US"));
            query["end_period"] = date.ToString("yyyy-MM-dd", new CultureInfo("en-US"));
            query["currency"] = currency;

            var header = new NameValueCollection();
            header["api-key"] = Appconfig.BOTAPIKey;
            var callResult = await RESTServiceCall.GetJSONAsync(Appconfig.BOTServiceEndPoint, query, header);

            dynamic response = callResult;
            if (response.result.success == "true")
            {
                try
                {
                    var data = response.result.data["data_detail"].First;
                    string period = data.period;
                    string currency_pair = (string)data.currency_id;
                    string buy = data[Appconfig.BuyValue];
                    if (String.IsNullOrWhiteSpace(buy)) buy = "0";
                    string sell = data[Appconfig.SellValue];
                    if (String.IsNullOrWhiteSpace(sell)) sell = "0";
                    log.Debug("GetRate Extracting Data complete at " + period + " " + currency_pair + " " + buy + " " + sell);

                    bool isAPIComplete = !String.IsNullOrWhiteSpace(period);
                    if (!isAPIComplete)
                    {
                        var timestampDate = DateTime.ParseExact((string)response.result.timestamp, "yyyy-MM-dd HH:mm:ss", new CultureInfo("en-US"));
                        var lastupdated = DateTime.ParseExact((string)response.result.data.data_header.last_updated, "yyyy-MM-dd", new CultureInfo("en-US"));

                        //ถ้า date เป็นของวันก่อนหน้า ถือว่าา APIComplete
                        //หรือถ้า date เป็นของวันนี้ และ timestamp บอกว่าเลย 6 โมงเย็น จะถือว่า complete เช่นกัน
                        if (date.CompareTo(timestampDate.Date) < 0) isAPIComplete = true;
                        else
                        {
                            if (timestampDate.Hour >= 18) isAPIComplete = true;
                        }
                    }

                    return new Result<CurrencyRate> { Success = true, Data = new CurrencyRate { Date = date, Buy = Convert.ToDecimal(buy), Sell = Convert.ToDecimal(sell), isAPIComplete = isAPIComplete, isSyncSAP = false, Currency = currency_pair } };
                }
                catch (Exception ex)
                {
                    log.Debug("GetRate REST calling pass but extracting data error" + ex.Message);
                    ExceptionHandling.LogException(ex);
                    return new Result<CurrencyRate> { Success = false, Failure = FailureType.UnexpectedServiceBehaviorError, Message = ex.Message };
                }

            }
            else
            {
                string errorMsg = string.Empty;
                JToken errors = response.result["error"] as JToken;
                foreach (dynamic error in errors)
                {
                    errorMsg += string.Format(@" {0}:{1} ", error.code, error.message);
                }
                log.Debug("GetRate REST calling ERROR Occured" + errorMsg);
                return new Result<CurrencyRate> { Success = false, Failure = FailureType.DatabaseConnectionError, Message = errorMsg };
            }

        }

        public static async Task<Result<List<DailyLog>>> GetRatePatching(string currency, DateTime dateStart, DateTime dateEnd)
        {
            log.Debug("GetRate Patching is run for " + currency + " " + dateStart.ToString("dd/MM/yyyy") + " to " + dateEnd.ToString("dd/MM/yyyy"));
            var query = new NameValueCollection();
            query["start_period"] = dateStart.ToString("yyyy-MM-dd", new CultureInfo("en-US"));
            query["end_period"] = dateEnd.ToString("yyyy-MM-dd", new CultureInfo("en-US"));
            query["currency"] = currency;

            var header = new NameValueCollection();
            header["api-key"] = Appconfig.BOTAPIKey;
            var callResult = await RESTServiceCall.GetJSONAsync(Appconfig.BOTServiceEndPoint, query, header);

            dynamic response = callResult;
            if (response.result.success == "true")
            {
                try
                {
                    var output = new List<DailyLog>();
                    foreach (var data in response.result.data["data_detail"])
                    {
                        string period = data.period;
                        string currency_pair = (string)data.currency_id;
                        string buy = data[Appconfig.BuyValue];
                        if (String.IsNullOrWhiteSpace(buy)) buy = "0";
                        string sell = data[Appconfig.SellValue];
                        if (String.IsNullOrWhiteSpace(sell)) sell = "0";
                        log.Debug("GetRatePatching Extracting Data complete at " + period + " " + currency_pair + " " + buy + " " + sell);
                        if (!String.IsNullOrWhiteSpace(period))
                        {
                            var date = DateTime.ParseExact(period, "yyyy-MM-dd", new CultureInfo("en-US"));
                            DailyLog log = new DailyLog()
                            {
                                CurrenciesRate = new List<CurrencyRate>
                                {
                                    new CurrencyRate { Date = date, Buy = Convert.ToDecimal(buy), Sell = Convert.ToDecimal(sell), isAPIComplete = true, isSyncSAP = false, Currency = currency_pair }
                                }
                                ,
                                Date = date
                            };
                            output.Add(log);
                        }
                    }
                    return new Result<List<DailyLog>> { Success = true, Data = output };
                }
                catch (Exception ex)
                {
                    log.Debug("GetRate REST calling pass but extracting data error" + ex.Message);
                    ExceptionHandling.LogException(ex);
                    return new Result<List<DailyLog>> { Success = false, Failure = FailureType.UnexpectedServiceBehaviorError, Message = ex.Message };
                }

            }
            else
            {
                string errorMsg = string.Empty;
                JToken errors = response.result["error"] as JToken;
                foreach (dynamic error in errors)
                {
                    errorMsg += string.Format(@" {0}:{1} ", error.code, error.message);
                }
                log.Debug("GetRate REST calling ERROR Occured" + errorMsg);
                return new Result<List<DailyLog>> { Success = false, Failure = FailureType.DatabaseConnectionError, Message = errorMsg };
            }

        }
    }
}
