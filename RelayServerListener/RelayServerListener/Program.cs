using System;
// below is required due to the use of CancellationTokenSource
using System.Threading; 
using System.Threading.Tasks;
// below is required due to use of StreamWriter and StreamReader
using System.IO;
// below requires that you add the Microsoft Azure Relay Nuget Package
using Microsoft.Azure.Relay;

namespace RelayServerListener
{
    class Program
    {
        private const string RelayNamespace = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string ConnectionName = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private const string KeyName = "RootManageSharedAccessKey";
        private const string myKey = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        public static void Main(string[] args)
        {
            RunAsync().GetAwaiter().GetResult();
        }

        private static async Task RunAsync()
        {
            var cts = new CancellationTokenSource();

          //  var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(KeyName, myKey);
          //  var listener = new HybridConnectionListener(new Uri(string.Format("sb://{0}/{1}", RelayNamespace, ConnectionName)), tokenProvider);

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(KeyName, myKey);
            var listener = new HybridConnectionListener(new Uri(String.Format("sb://{0}/{1}", RelayNamespace, ConnectionName)), tokenProvider);


            // Subscribe to the status events
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            listener.Connecting += (o, e) => { Console.WriteLine("Connecting"); };
            listener.Offline += (o, e) => { Console.WriteLine("Offline"); };
            listener.Online += (o, e) => { Console.WriteLine("Online"); };

            // Opening the listener will establish the control channel to
            // the Azure Relay service. The control channel will be continuously 
            // maintained and reestablished when connectivity is disrupted.
            await listener.OpenAsync(cts.Token);
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Server " + RelayNamespace + " is listening...");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            
            // Providing callback for cancellation token that will close the listener.
            cts.Token.Register(() => listener.CloseAsync(CancellationToken.None));

            // Start a new thread that will continuously read the console.
            new Task(() => Console.In.ReadLineAsync().ContinueWith((s) => { cts.Cancel(); })).Start();

            // Accept the next available, pending connection request. 
            // Shutting down the listener will allow a clean exit with 
            // this method returning null
            while (true)
            {
                var relayConnection = await listener.AcceptConnectionAsync();
                if (relayConnection == null)
                {
                    break;
                }

                ProcessMessagesOnConnection(relayConnection, cts);
            }

            // Close the listener after we exit the processing loop
            await listener.CloseAsync(cts.Token);
        }

        private static async void ProcessMessagesOnConnection(HybridConnectionStream relayConnection, CancellationTokenSource cts)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("New session established...");
            Console.ForegroundColor = ConsoleColor.White;

            // The connection is a fully bidrectional stream. 
            // We put a stream reader and a stream writer over it 
            // which allows us to read UTF-8 text that comes from 
            // the sender and to write text replies back.
            var reader = new StreamReader(relayConnection);
            var writer = new StreamWriter(relayConnection) { AutoFlush = true };

            // Write the line back to the client, prepending "Echo:"
            await writer.WriteLineAsync("Hello client from the server!");

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    // Read a line of input until a newline is encountered
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                    {
                        // If there's no input data, we will signal that 
                        // we will no longer send data on this connection
                        // and then break out of the processing loop.
                        await relayConnection.ShutdownAsync(cts.Token);
                        break;
                    }

                    // Output the line on the console
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Received from client: " + line);
                    Console.ForegroundColor = ConsoleColor.White;

                    // Write the line back to the client, prepending "Echo:"
                    await writer.WriteLineAsync($"Thanks for sending: {line}");
                }
                catch (Exception ex)
                {
                    // Catch an IO exception that is likely caused because
                    // the client disconnected.
                    Console.WriteLine("Looks like the client closed connection...");
                    break;
                }
            }

            Console.WriteLine("Session ended with client...");

            // Closing the connection
            await relayConnection.CloseAsync(cts.Token);
        }
    }
}


   