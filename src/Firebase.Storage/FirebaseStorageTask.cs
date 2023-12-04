namespace Firebase.Storage
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class FirebaseStorageTask
    {
        private const int ProgressReportDelayMiliseconds = 500;

        private readonly Task<string> uploadTask;
        private readonly Stream stream;
        private HttpClient HttpClient;

        public FirebaseStorageTask(HttpClient client, FirebaseStorageOptions options, string url, string downloadUrl, Stream stream, CancellationToken cancellationToken, string mimeType = null)
        {
            this.HttpClient = client;
            this.TargetUrl = url;
            this.uploadTask = this.UploadFile(options, url, downloadUrl, stream, cancellationToken, mimeType);
            this.stream = stream;
            this.Progress = new Progress<FirebaseStorageProgress>();

            Task.Factory.StartNew(this.ReportProgressLoop);
        }

        public Progress<FirebaseStorageProgress> Progress
        {
            get;
            private set;
        }

        public string TargetUrl
        {
            get;
            private set;
        }

        public TaskAwaiter<string> GetAwaiter()
        {
            return this.uploadTask.GetAwaiter();
        }

        private async Task<string> UploadFile(FirebaseStorageOptions options, string url, string downloadUrl, Stream stream, CancellationToken cancellationToken, string mimeType = null)
        {
            var responseData = "N/A";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StreamContent(stream)
                };

                if(options.AuthTokenAsyncFactory != null)
                {
                    var auth = await options.AuthTokenAsyncFactory().ConfigureAwait(false);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Firebase", auth);
                }

                if (!string.IsNullOrEmpty(mimeType))
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                }

                if(options.CustomHeadersProvider != null)
                {
                    foreach(var customHeader in await options.CustomHeadersProvider())
                    {
                        request.Headers.Add(customHeader.name, customHeader.value);
                    }
                }

                var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseData);

                return downloadUrl + data["downloadTokens"];
            }
            catch (TaskCanceledException)
            {
                if (options.ThrowOnCancel)
                {
                    throw;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                throw new FirebaseStorageException(url, responseData, ex);
            }
        }

        private async void ReportProgressLoop()
        {
            while (!this.uploadTask.IsCompleted)
            {
                await Task.Delay(ProgressReportDelayMiliseconds);

                try
                { 
                    this.OnReportProgress(new FirebaseStorageProgress(this.stream.Position, this.stream.Length));
                }
                catch (ObjectDisposedException)
                {
                    // there is no 100 % way to prevent ObjectDisposedException, there are bound to be concurrency issues.
                    return;
                } 
            }
        }

        private void OnReportProgress(FirebaseStorageProgress progress)
        {
            (this.Progress as IProgress<FirebaseStorageProgress>).Report(progress);
        }
    }
}
