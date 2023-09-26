using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Storage
{
    public static class HttpRequestMessageHelper
    {
        public static async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(this FirebaseStorageOptions options,HttpMethod method,string url)
        {
            var requestMessage = new HttpRequestMessage(method,url);
            if(options.AuthTokenAsyncFactory != null)
            {
                var auth = await options.AuthTokenAsyncFactory().ConfigureAwait(false);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Firebase", auth);
            }

            return requestMessage;
        }
    }
}
