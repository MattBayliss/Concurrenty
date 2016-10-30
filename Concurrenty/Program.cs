using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Concurrenty
{
    class Program
    {
        static void Main(string[] args)
        {
            var renderer = new Renderer(20, RenderRequest);

            var tasks = Task.Run(async () =>
            {
                var responseTasks = new Task<Response>[40];
                for (int i = 0; i < responseTasks.Length; i++)
                {
                    var request = new Request { Id = (100 + i) };
                    responseTasks[i] = renderer.Render(request, TimeSpan.FromSeconds(30));
                }
                await Task.WhenAll(responseTasks.Where(t => t != null));
            });

            Console.ReadLine();

            renderer.Stop();

            Console.ReadLine();

        }

        private static async Task<Response> RenderRequest(Request request, CancellationToken ct)
        {
            Console.WriteLine("{0}: Render Started", request.Id);

            // Here would be the heavy lifting of transforming a request into a response.
            // We'll just wait a random time between 2 and 20 seconds
            var random = new Random().Next(2000, 20000);
            await Task.Delay(random, ct);
            if(ct.IsCancellationRequested)
            {
                Console.WriteLine("{0}: Render cancelled", request.Id);
                return null;
            }

            Console.WriteLine("{0}: Render Finished", request.Id);

            return new Response
            {
                Filename = Path.GetTempFileName(),
                Success = true
            };

        }
    }
}
