using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// below is required due to the use of CancellationTokenSource
using System.Threading;
// below is required due to use of StreamWriter and StreamReader
using System.IO;
// below requires that you add the Microsoft Azure Relay Nuget Package
using Microsoft.Azure.Relay;

namespace RelayClientSender
{
    class Program
    {
        private const string RelayNamespace = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
        private const string ConnectionName = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
        private const string KeyName = "RootManageSharedAccessKey";
        private const string myKey = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        static void Main(string[] args)
        {
            try { 
                RunAsync().GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Console.WriteLine("\nAn error occurred trying to communicate with server " + RelayNamespace + ":\n\t" + ex.InnerException);
                Console.WriteLine("\nHit ENTER to exit");
                Console.ReadLine();
            }

        }

        private static async Task RunAsync()
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Client ready");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Enter lines of text to send to the server with ENTER");

            // Create a new hybrid connection client
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(KeyName, myKey);
            var client = new HybridConnectionClient(new Uri(String.Format("sb://{0}/{1}", RelayNamespace, ConnectionName)), tokenProvider);

            // Initiate the connection
            var relayConnection = await client.CreateConnectionAsync();

            try {
                // We run two conucrrent loops on the connection. One 
                // reads input from the console and writes it to the connection 
                // with a stream writer. The other reads lines of input from the 
                // connection with a stream reader and writes them to the console. 
                // Entering a blank line will shut down the write task after 
                // sending it to the server. The server will then cleanly shut down
                // the connection which will terminate the read task.

                var reads = Task.Run(async () => {
                    // Initialize the stream reader over the connection
                    var reader = new StreamReader(relayConnection);
                    var writer = Console.Out;

                    do
                    {
                        // Read a full line of UTF-8 text up to newline
                        string line = await reader.ReadLineAsync();
                        // if the string is empty or null, we are done.
                        if (String.IsNullOrEmpty(line))
                            break;
                        // Write to the console
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        await writer.WriteLineAsync(line);
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    while (true);
                });

                // Read from the console and write to the hybrid connection
                var writes = Task.Run(async () => {
                    var reader = Console.In;
                    var writer = new StreamWriter(relayConnection) { AutoFlush = true };
                    do
                    {
                        // Read a line form the console
                        string line = await reader.ReadLineAsync();
                        // Write the line out, also when it's empty
                        await writer.WriteLineAsync(line);
                        // Quit when the line was empty
                        if (String.IsNullOrEmpty(line))
                            break;
                    }
                    while (true);
                });

                // Wait for both tasks to complete
                await Task.WhenAll(reads, writes);
                await relayConnection.CloseAsync(CancellationToken.None);
            }
            catch(Exception ex)
            {
                Console.WriteLine("\n\nAn error occurred trying to reach the server:\n" + ex.InnerException);
                Console.WriteLine("Hit ENTER to exit");
                Console.ReadLine();
            }
         }

    }
}
