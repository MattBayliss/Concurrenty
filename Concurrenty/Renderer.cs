using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Concurrenty
{
    public class Renderer
    {
        private Task _processQueueTask;
        private ConcurrentQueue<Request> _requestQueue;
        private ConcurrentDictionary<int, Task<Response>> _responseTasks;
        private int _threadCount;
        private CancellationTokenSource _stopToken;
        private readonly static object QueueLock = new object();
        private Func<Request, CancellationToken, Task<Response>> _renderFunc;

        public Renderer(int threadCount, Func<Request, CancellationToken, Task<Response>> renderFunc)
        {
            _threadCount = threadCount;
            _renderFunc = renderFunc;

            _processQueueTask = null;
            _requestQueue = new ConcurrentQueue<Request>();
            _responseTasks = new ConcurrentDictionary<int, Task<Response>>();
            _stopToken = new CancellationTokenSource();
        }

        public void Stop()
        {
            _stopToken.Cancel();
        }

        public async Task<Response> Render(Request request, TimeSpan timeout)
        {
            await EnqueueRequestAndStartProcessing(request, _stopToken.Token);
            //request should now be registered

            if(_stopToken.Token.IsCancellationRequested)
            {
                return null;
            }

            Task<Response> responseTask;
            if (!_responseTasks.TryRemove(request.Id, out responseTask))
            {
                throw new ApplicationException("Failed to process");
            };

            if (responseTask == await Task.WhenAny(responseTask, Task.Delay(timeout, _stopToken.Token)))
            {
                return await responseTask;
            }
            else
            {
                Console.WriteLine("{0}: Render failed to complete in a timely fashion", request.Id);
                return null;
            }
        }

        private async Task EnqueueRequestAndStartProcessing(Request request, CancellationToken ct)
        {
            _requestQueue.Enqueue(request);

            if ((_processQueueTask == null) || (_processQueueTask.IsCompleted))
            {
                _processQueueTask = ProcessQueue(ct);
            }
            await _processQueueTask;
        }

        private async Task ProcessQueue(CancellationToken ct)
        {
            Console.WriteLine(">>> PROCESSING QUEUE");

            // allow 3 seconds between processing so the queue can fill up a bit
            await Task.Delay(3000, ct);

            var tasks = new Task[_threadCount];

            // tasks index
            var t = 0;

            // while there are requests in the queue, cycle through our tasks array to see if there's a spot free in our "Task pool"
            while (!_requestQueue.IsEmpty && !ct.IsCancellationRequested)
            {
                Request request;
                if (_requestQueue.TryDequeue(out request))
                {
                    Console.WriteLine("{0}: request dequeued", request.Id);
                    bool allocatedToTask = false;
                    while (!allocatedToTask && !ct.IsCancellationRequested)
                    {
                        // find a free spot, starting at the last task assigned (t)
                        for (int i = 0; i < _threadCount; i++)
                        {
                            t = (t + i) % _threadCount;
                            if ((tasks[t] == null) || (tasks[t].IsCompleted))
                            {
                                allocatedToTask = true;
                                tasks[t] = _responseTasks.GetOrAdd(request.Id, _renderFunc(request, ct));
                                Console.WriteLine("{0}: request allocated to task {1}", request.Id, t);
                                break;
                            }
                        }
                        if (!allocatedToTask && !ct.IsCancellationRequested)
                        {
                            Console.WriteLine("WAITING FOR A FREE TASK...");
                            // all tasks are busy, need to wait for one to become available
                            await Task.WhenAny(tasks);
                        }
                    }
                }
                else
                {
                    Console.WriteLine(">>> failed to dequeue request - waiting 100ms");
                    await Task.Delay(100);
                }
            }
            // queue is empty - all requests processed. Wait for the results
            Console.WriteLine(">>> QUEUE CLEARED");
            await Task.WhenAll(tasks.Where(task => task != null));
        }               
    }
}
