using System.Collections.Concurrent;
using LibrarySystem.Utils;

namespace LibrarySystem.Services
{
    public class RequestQueueService
    {
        private readonly ConcurrentQueue<HttpRequestMessage> _requestMessagesQueue = new();
        private readonly HttpClient _httpClient = new();
        private const int TimeoutInSeconds = 10;
        private static object locker = new();
        private readonly CircuitBreaker _circuitBreaker;

        public RequestQueueService()
        {
            _circuitBreaker = CircuitBreaker.Instance;
        }

        public void StartWorker()
        {
            new Thread(Start).Start();
            Console.WriteLine("Thread was started");
        }

        public void AddRequestToQueue(HttpRequestMessage httpRequestMessage)
        {
            _requestMessagesQueue.Enqueue(httpRequestMessage);
        }

        private async void Start(object? state)
        {
            while (true)
            {
                lock (locker)
                {
                    if (!_requestMessagesQueue.TryPeek(out var req))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    try
                    {
                        var res = _httpClient.Send(req);
                        if (res.IsSuccessStatusCode)
                        {
                            _requestMessagesQueue.TryDequeue(out _);
                            _circuitBreaker.ResetFailureCount();
                        }
                        else
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(TimeoutInSeconds));
                        }
                    }
                    catch (Exception e)
                    {
                        var reqClone = HttpRequestMessageHelper.CloneHttpRequestMessageAsync(req).GetAwaiter().GetResult();
                        _requestMessagesQueue.TryDequeue(out _);
                        _requestMessagesQueue.Enqueue(reqClone);

                        Thread.Sleep(TimeSpan.FromSeconds(TimeoutInSeconds));
                    }
                }
            }
        }
    }
}