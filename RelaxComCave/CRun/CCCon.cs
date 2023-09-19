using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RelaxComCave.Runner {
    public abstract class CCCon {

        /// <summary>
        /// Enthält die Default Request Headers
        /// </summary>
        private IReadOnlyDictionary<string, string> DefaultRequestHeaders = new Dictionary<string, string>() {
            {"Sec-Ch-Ua","\"Chromium\";v=\"116\", \"Not)A;Brand\";v=\"24\", \"Google Chrome\";v=\"116\"" },
            {"Sec-Ch-Ua-Mobile","?0" },
            {"Sec-Ch-Ua-Platform","\"Windows\"" },
            {"Sec-Fetch-Dest","document" },
            {"Sec-Fetch-Mode","navigate" },
            {"Sec-Fetch-Site","same-origin" },
            {"Sec-Fetch-User","?1" },
            {"Upgrade-Insecure-Requests","1" },
            {"User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36" }
        };

        /// <summary>
        /// Speicher der aktuellen Session Headers
        /// </summary>
        protected Dictionary<string,string> Headers { get; set; }

        /// <summary>
        /// Konstruktor
        /// </summary>
        protected CCCon() { Headers = new Dictionary<string, string>(); }

        /// <summary>
        /// Führt einen einfachen GET Call aus.
        /// </summary>
        /// <param name="url">Die zu verwendende URL</param>
        /// <param name="AdditionalHeaders"></param>
        /// <returns></returns>
        protected async Task<HttpResponseMessage?> GET_Request(string url, Dictionary<string,string>? AdditionalHeaders = null) {
            try {
                using (var client = new HttpClient()) {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    foreach (var h in Headers) request.Headers.Add(h.Key, h.Value);
                    foreach (var h in DefaultRequestHeaders) request.Headers.Add(h.Key, h.Value);
                    if (AdditionalHeaders != null) foreach (var h in AdditionalHeaders) request.Headers.Add(h.Key, h.Value);
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            } catch (Exception) { return null; }
        } 

        /// <summary>
        /// Führt einen Einfachen POST Call aus.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="content"></param>
        /// <param name="AdditionalHeaders"></param>
        /// <returns></returns>
        protected async Task<HttpResponseMessage?> POST_Request(string url, string message, Dictionary<string,string>? AdditionalHeaders = null) {
            try {
                using (var client = new HttpClient()) { 
                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                var content = new StringContent(message, null, "application/x-www-form-urlencoded");
                foreach (var h in Headers) request.Headers.Add(h.Key, h.Value);
                foreach (var h in DefaultRequestHeaders) request.Headers.Add(h.Key, h.Value);
                if (AdditionalHeaders != null) foreach (var h in AdditionalHeaders) request.Headers.Add(h.Key, h.Value);
                request.Content = content;
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return response;
                }
            } catch(Exception) { return null; }
        }
    }
}
