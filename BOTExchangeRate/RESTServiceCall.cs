using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
}
