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
            if ((bool)response.result.success)
            {
                try
                {
                    dynamic data = response.data.data_detail;//first element
                    string period = (string)data.period;
                    string currency_pair = (string)data.currency_id;
                    string buy = (string)data.GetType().GetProperty(Appconfig.BuyValue).GetValue(data, null);
                    string sell = (string)data.GetType().GetProperty(Appconfig.SellValue).GetValue(data, null);
                    log.Debug("GetRate Extracting Data complete at " + period + " " + currency_pair + " " + buy + " " + sell);
                    return new Result<CurrencyRate> { Success = true, Data = new CurrencyRate { Date = date, Buy = Convert.ToDecimal(buy), Sell = Convert.ToDecimal(sell), isAPIComplete = true, isSyncSAP = false, Currency = currency_pair } };//TODO
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
                log.Debug("GetRate REST calling ERROR Occured" + response.result.error.message);
                return new Result<CurrencyRate> { Success = false, Failure = FailureType.DatabaseConnectionError, Message = (string)response.result.error.message };
            }

        }
    }
}
