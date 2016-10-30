# Concurrenty

An experiment using C#, Microsoft.Net, the System.Collections.Concurrent library, and async await to create a multi-threaded queue processor. A full write-up of what's going on is at http://mattbayliss.com/c%23/2016/10/31/concurrent-asyncy-queues-ftw.html

## Usage

This project contains a Request and Response object that can be customised to your needs. This example assumes a Renderer module that will generate thumbnails of specified record IDs. So, the Request and Response objects are very simple:

	public class Request
    {
        public int Id { get; set; }
    }
	
	public class Response
	{
		public bool Success { get; set; }
		public string Filename { get; set; }
	}


Initialise the queue processor, Renderer, with the constructor

	Renderer(int threadCount, Func<Request, CancellationToken, Task<Response>> renderFunc)
	
*threadCount*: the number of threads to use.
*renderFunc*: the function that changes a Request into a Task<Response> - so this would be the function that generates the thumbnail.

Then, to add something to the queue and wait for a response, you call the Renderer.Render function:

	public async Task<Response> Render(Request request, TimeSpan timeout)

Here's an example:

	public async Task<string> RenderToThumbnail(int recordId)
	{
		string thumbnailPath = null;
		var renderer = new Renderer(20, RenderRequest);
		var request = new Request { Id = 123 };
		Response response = null;
		try
		{
			response = await renderer.Render(request, TimeSpan.FromSeconds(30));
		}
		catch(TimeoutException)
		{
			// timeout reached
		}

		return thumbnailPath;
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
