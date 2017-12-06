using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BOTExchangeRate
{
    class Program
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

            CallRESTAysnc().Wait();
        }

        private static async Task CallRESTAysnc()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            //https://stackoverflow.com/questions/33634605/not-receiving-response-after-postasync
            //https://developer.salesforce.com/page/Working_with_Custom_SOAP_and_REST_Services_in_.NET_Applications
            using (HttpClient client = new HttpClient())
            {
                //the line below enables TLS1.1 and TLS1.2 (Saleforce reject TLS1.0 which used in dot net framework 4.5.2)
                //defined remote access app - develop --> remote access --> new

                var builder = new UriBuilder("https://iapi.bot.or.th/Stat/Stat-ExchangeRate/DAILY_AVG_EXG_RATE_V1/");
                builder.Port = -1;
                var query = HttpUtility.ParseQueryString(builder.Query);
                query["start_period"] = "2017-06-30";
                query["end_period"] = "2017-06-30";
                query["currency"] = "USD";
                builder.Query = query.ToString();
                string url = builder.ToString();

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("api-key", "U9G1L457H6DCugT7VmBaEacbHV9RX0PySO05cYaGsm");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //request.Content = content;
                HttpResponseMessage response = await client.SendAsync(request);

                string responseString = await response.Content.ReadAsStringAsync();


                JObject obj = JObject.Parse(responseString);

            }
        }
    }
}
