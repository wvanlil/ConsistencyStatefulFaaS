using Custom;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using SmallBank.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ElleSnapperExperimentProcess
{

    public class Program2
    {
        static async Task<int> Main(string[] args)
        {
            //return RunMainAsync().Result;
            Orleans.IClusterClient client;

            try
            {
                client = await ConnectClient();
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nException while trying to run client: {e.Message}");
                Console.WriteLine("Make sure the silo the client is trying to connect to is running.");
                Console.WriteLine("\nStarting web server anyway.");
                client = null;
            }

            IHostBuilder host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/", async context =>
                            {
                                await context.Response.WriteAsync("Hello World! Custom client");
                            });

                            endpoints.MapPost("/", async context =>
                            {
                                var grainId = 0; // stateless?
                                var grain = client.GetGrain<IJepsenTransactionGrain>(grainId);
                                //var ret = grain.StartTransaction("ParseAndExecute", "[[:append 0 5] [:r 0 nil]]");
                                var grainAccessInfo = new Dictionary<int, Tuple<string, int>>();
                                grainAccessInfo.Add(0, new Tuple<string, int>("SmallBank.Grains", 1));

                                var ret = await grain.StartTransaction("ParseAndExecute", "[[:append 0 5]]", grainAccessInfo);
                                await context.Response.WriteAsync(ret.resultObject.ToString());
                            });

                            endpoints.MapPost("/2", async context =>
                            {
                                var grain = client.GetGrain<IJepsenTransactionGrain>(1);
                                var ret = await grain.StartTransaction("ParseAndExecute", "[[:r 0 nil]]");
                                await context.Response.WriteAsync(ret.resultObject.ToString());
                            });

                            endpoints.MapPost("/3", async context =>
                            {
                                var grain = client.GetGrain<IJepsenTransactionGrain>(0);
                                var ret = await grain.StartTransaction("ParseAndExecute", "[[:append 0 1]]");
                                await context.Response.WriteAsync(ret.resultObject.ToString());
                            });
                        });
                    });
                });
            var build = host.Build();
            Console.WriteLine("Starting web server");
            build.Run();
            Console.WriteLine("Webserver stopped");

            return 0;
        }

        public static async Task<IClusterClient> ConnectClient()
        {
            const string connectionString = Utilities.Constants.connectionString;

            IClusterClient client;
            client = new ClientBuilder()
                //.UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = Utilities.Constants.clusterId;
                    options.ServiceId = Utilities.Constants.serviceId;
                })
                .ConfigureLogging(logging => logging.AddConsole())

                .UseAzureStorageClustering(options =>
                {
                    options.ConnectionString = Utilities.Constants.connectionString;
                    //options.ConfigureTableServiceClient(connectionString);
                })

                .Build();

            await client.Connect();
            Console.WriteLine("Client successfully connected to silo host \n");
            return client;
        }

    }
}

