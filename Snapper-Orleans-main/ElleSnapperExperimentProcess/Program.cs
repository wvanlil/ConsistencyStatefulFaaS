﻿using System;
using Utilities;
using System.Threading;
using System.Collections.Generic;
using Concurrency.Interface;
using System.Diagnostics;
using NetMQ.Sockets;
using NetMQ;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using System.IO;

using Microsoft.AspNetCore;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SmallBank.Interfaces;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Orleans;
using System.Threading.Tasks;

namespace ElleSnapperExperimentProcess
{
    using SharedRequest = Dictionary<int, Queue<Tuple<bool, RequestData>>>;

    public class JsonData
    {
        public string operations { get; set; }
    }

    class Program
    {
        static int experimentID;
        static SiloConfiguration siloConfig;
        static string siloPublicIPAddress;

        static bool asyncConnectionDone = false;
        static bool asyncInitializationDone = false;
        static WorkloadGroup workloadGroup;
        static bool experimentDone = false;
        static bool tableDeleted = false;

        static bool asyncMultipleClients = false; // custom

        static void Main(string[] args)
        {
            // =======================================================================================
            //experimentID = int.Parse(args[0]);
            //var numCPUPerSilo = int.Parse(args[1]);
            //var implementationType = Enum.Parse<ImplementationType>(args[2]);
            //var benchmarkType = Enum.Parse<BenchmarkType>(args[3]);
            //var loggingEnabled = bool.Parse(args[4]);
            experimentID = 1337;
            var numCPUPerSilo = 4;
            var implementationType = Enum.Parse<ImplementationType>("SNAPPER");
            var benchmarkType = Enum.Parse<BenchmarkType>("SMALLBANK");
            var loggingEnabled = true;

            var NUM_OrderGrain_PER_D = 0;
            if (benchmarkType == BenchmarkType.TPCC) NUM_OrderGrain_PER_D = int.Parse(args[5]);
            siloConfig = new SiloConfiguration(numCPUPerSilo, implementationType, benchmarkType, loggingEnabled, NUM_OrderGrain_PER_D);
            Console.WriteLine($"Fig.{experimentID}, silo_vCPU = {numCPUPerSilo}, implementation = {implementationType}, benchmark = {benchmarkType}, loggingEnabled = {loggingEnabled}");

            // =======================================================================================
            Console.WriteLine("Set processor affinity for SnapperExperimentProcess...");
            var processes = Process.GetProcessesByName("ElleSnapperExperimentProcess");
            processes = new Process[] { Process.GetCurrentProcess() }; // Dont care about multiple processes with same name...
            //var processes = Process.GetProcessesByName("iisexpress");
            Debug.Assert(processes.Length == 1);   // there is only one process called "SnapperExperimentProcess"

            var str = Helper.GetWorkerProcessorAffinity(numCPUPerSilo);
            var clientProcessorAffinity = Convert.ToInt64(str, 2);     // client uses the lowest n bits
            processes[0].ProcessorAffinity = (IntPtr)clientProcessorAffinity;

            // =======================================================================================
            var orleansClientManager = new OrleansClientManager(siloConfig);
            ConnectToSilo(orleansClientManager);
            while (!asyncConnectionDone) Thread.Sleep(100);

            // =======================================================================================
            var serializer = new MsgPackSerializer();
            string inputSocketAddress;
            string outputSocketAddress;
            if (Constants.LocalCluster)
            {
                inputSocketAddress = Constants.Worker_LocalCluster_InputSocket;
                outputSocketAddress = Constants.Worker_LocalCluster_OutputSocket;
            }
            else
            {
                inputSocketAddress = ">tcp://" + siloPublicIPAddress + ":" + Constants.siloOutPort;
                outputSocketAddress = ">tcp://" + siloPublicIPAddress + ":" + Constants.siloInPort;
            }

            var inputSocket = new SubscriberSocket(inputSocketAddress);
            var outputSocket = new PushSocket(outputSocketAddress);
            inputSocket.Subscribe("");
            outputSocket.SendFrame(serializer.serialize(NetworkMessage.CONNECTED));
            Console.WriteLine("Connect to Silo. ");

            inputSocket.Options.ReceiveHighWatermark = 1000;
            //var msg = serializer.deserialize<NetworkMessage>(inputSocket.ReceiveFrameBytes());
            //Trace.Assert(msg == NetworkMessage.CONFIRMED);
            //Console.WriteLine("Receive confirmation message from Silo. ");
            Console.WriteLine("Not waiting for silo message");

            // =======================================================================================
            //LoadGrains(orleansClientManager);
            //while (!asyncInitializationDone) Thread.Sleep(100);


            // custom web server

            //var myclient = orleansClientManager.StartClientWithRetries();

            static async Task<Stack<IClusterClient>> AsyncMultipleClients()
            {
                Stack<IClusterClient> clients = new Stack<IClusterClient>();
                for (int i = 0; i < 5; i++)
                {
                    var stackOrleansClientManager = new OrleansClientManager(siloConfig);
                    var curClient = await stackOrleansClientManager.StartClientWithRetries();
                    clients.Push(curClient);
                }

                asyncMultipleClients = true;

                return clients;
            }
            var myclients = AsyncMultipleClients();
            while (!asyncMultipleClients) Thread.Sleep(100);
            Stack<IClusterClient> clientStack = myclients.Result;

            Random r = new Random();
            IHostBuilder host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost("/pact", async context =>
                            {
                                if (clientStack.Count == 0)
                                {
                                    Console.WriteLine("No client available");
                                    throw new Exception("No clients available");
                                }

                                IClusterClient client = null;

                                try
                                {
                                    client = clientStack.Pop();
                                    Console.WriteLine("After popping pact " + client.ToString() + ": " + clientStack.Count.ToString());

                                    var stream = new StreamReader(context.Request.Body);
                                    var body = await stream.ReadToEndAsync();

                                    JsonData json = JsonConvert.DeserializeObject<JsonData>(body);

                                    //  [0-9]* 
                                    // https://regexr.com/
                                    // Whitespace included!
                                    // [[:r 8 nil] [:append 9 2]]
                                    Regex regex = new Regex(" [0-9]* ");
                                    var grainAccessInfo = new Dictionary<int, Tuple<string, int>>();

                                    MatchCollection matches = regex.Matches(json.operations);
                                    var accessMap = new Dictionary<int, int>();
                                    foreach (Match match in matches)
                                    {
                                        int accessed = int.Parse(match.Value.Substring(1, match.Value.Length - 2));
                                        if (!accessMap.ContainsKey(accessed))
                                        {
                                            accessMap.Add(accessed, 1);
                                            continue;
                                        }
                                        accessMap[accessed]++;
                                    }
                                    int TGrain = r.Next(1000, 100000);
                                    grainAccessInfo.Add(TGrain, new Tuple<string, int>("SmallBank.Grains.JepsenTransactionKeyGrain", 1));
                                    foreach (KeyValuePair<int, int> entry in accessMap)
                                    {
                                        grainAccessInfo.Add(entry.Key, new Tuple<string, int>("SmallBank.Grains.DataGrain", entry.Value));
                                    }
                                    
                                    var grain = client.GetGrain<IJepsenTransactionKeyGrain>(TGrain);
                                    clientStack.Push(client);
                                    Console.WriteLine("After pushing pact (crash) " + client.ToString() + ": " + clientStack.Count.ToString());
                                    client = null;
                                    var ret = await grain.StartTransaction("ParseAndExecute", json.operations, grainAccessInfo);

                                    await context.Response.WriteAsync(ret.resultObject.ToString());
                                } catch (Exception ex)
                                {
                                    if (client != null)
                                    {
                                        clientStack.Push(client);
                                        Console.WriteLine("After pushing pact " + client.ToString() + ": " + clientStack.Count.ToString());
                                        Console.WriteLine(ex.ToString());
                                    }
                                    throw new Exception(ex.Message);
                                }
                                
                            });

                            endpoints.MapPost("/act", async context =>
                            {
                                if (clientStack.Count == 0)
                                {
                                    Console.WriteLine("No client available");
                                    throw new Exception("No clients available");
                                }

                                IClusterClient client = null;

                                try
                                {
                                    client = clientStack.Pop();
                                    Console.WriteLine("After popping act " + client.ToString() + ": " + clientStack.Count.ToString());

                                    var stream = new StreamReader(context.Request.Body);
                                    var body = await stream.ReadToEndAsync();

                                    JsonData json = JsonConvert.DeserializeObject<JsonData>(body);

                                    int TGrain = r.Next(1000, 100000);
                                    var grain = client.GetGrain<IJepsenTransactionKeyGrain>(TGrain);
                                    var ret = await grain.StartTransaction("ParseAndExecuteGroup", json.operations);
                                    clientStack.Push(client);
                                    Console.WriteLine("After pushing act " + client.ToString() + ": " + clientStack.Count.ToString());

                                    client = null;
                                    await context.Response.WriteAsync(ret.resultObject.ToString());
                                } catch (Exception ex)
                                {
                                    if (client != null)
                                    {
                                        clientStack.Push(client);
                                        Console.WriteLine("After pushing act (crash) " + client.ToString() + ": " + clientStack.Count.ToString());
                                        Console.WriteLine(ex.ToString());
                                    }
                                    throw new Exception(ex.Message);
                                }

                            });

                            endpoints.MapPost("/act-nogroup", async context =>
                            {
                                if (clientStack.Count == 0)
                                {
                                    Console.WriteLine("No client available");
                                    throw new Exception("No clients available");
                                }

                                IClusterClient client = null;

                                try
                                {
                                    client = clientStack.Pop();
                                    Console.WriteLine("After popping act " + client.ToString() + ": " + clientStack.Count.ToString());

                                    var stream = new StreamReader(context.Request.Body);
                                    var body = await stream.ReadToEndAsync();

                                    JsonData json = JsonConvert.DeserializeObject<JsonData>(body);

                                    int TGrain = r.Next(1000, 100000);
                                    var grain = client.GetGrain<IJepsenTransactionKeyGrain>(TGrain);
                                    var ret = await grain.StartTransaction("ParseAndExecute", json.operations);
                                    clientStack.Push(client);
                                    Console.WriteLine("After pushing act " + client.ToString() + ": " + clientStack.Count.ToString());

                                    client = null;
                                    await context.Response.WriteAsync(ret.resultObject.ToString());
                                }
                                catch (Exception ex)
                                {
                                    if (client != null)
                                    {
                                        clientStack.Push(client);
                                        Console.WriteLine("After pushing act (crash) " + client.ToString() + ": " + clientStack.Count.ToString());
                                        Console.WriteLine(ex.ToString());
                                    }
                                    throw new Exception(ex.Message);
                                }

                            });
                        });
                    });
                });
            var build = host.Build();
            Console.WriteLine("Starting web server");
            build.Run();
            Console.WriteLine("Webserver stopped");




            Console.WriteLine("Get workload settings and run experiments...");
            GetWorkloadSetting();

            RunExperiments();
            while (!experimentDone) Thread.Sleep(100);
            Console.WriteLine("Finished running experiment. ");

            // =======================================================================================
            inputSocket.Subscribe("");
            outputSocket.SendFrame(serializer.serialize(NetworkMessage.SIGNAL));
            Console.WriteLine("Send message to Silo to inform the completion of experiment. ");

            inputSocket.Options.ReceiveHighWatermark = 1000;
            var msg = serializer.deserialize<NetworkMessage>(inputSocket.ReceiveFrameBytes());
            Trace.Assert(msg == NetworkMessage.CONFIRMED);
            Console.WriteLine("Receive confirmation message from Silo. ");
            /*
            // delete silo membership table from DynamoDB
            if (Constants.localCluster) return;
            Console.WriteLine("Delete membership table from DynamoDB...");
            DeleteDynamoDBTable();
            while (!tableDeleted) Thread.Sleep(100);*/
        }
        /*
        static async void DeleteDynamoDBTable()
        {
            var dynamoDBClient = new AmazonDynamoDBClient(Constants.AccessKey, Constants.SecretKey, Amazon.RegionEndpoint.USEast1);
            var request = new DeleteTableRequest { TableName = Constants.SiloMembershipTable };
            await dynamoDBClient.DeleteTableAsync(request);
            tableDeleted = true;
        }*/

        static async void ConnectToSilo(OrleansClientManager orleansClientManager)
        {
            var client = await orleansClientManager.StartClientWithRetries();

            var isSnapper = siloConfig.implementationType == ImplementationType.SNAPPER;
            var configGrain = client.GetGrain<IConfigurationManagerGrain>(0);
            siloPublicIPAddress = await configGrain.Initialize(isSnapper, siloConfig.numCPUPerSilo, siloConfig.loggingEnabled);
            if (siloConfig.benchmarkType == BenchmarkType.TPCC) await configGrain.InitializeTPCCManager(siloConfig.NUM_OrderGrain_PER_D);
            Console.WriteLine($"Spawned the configuration grain, get silo public IP address {siloPublicIPAddress}");

            asyncConnectionDone = true;
        }

        static async void LoadGrains(OrleansClientManager orleansClientManager)
        {
            if (siloConfig.benchmarkType == BenchmarkType.SMALLBANK || siloConfig.benchmarkType == BenchmarkType.NEWSMALLBANK) 
                await orleansClientManager.LoadSmallBankGrains(siloConfig.loggingEnabled);
            else if (siloConfig.benchmarkType == BenchmarkType.TPCC) 
                await orleansClientManager.LoadTPCCGrains();

            asyncInitializationDone = true;
        }

        static void GetWorkloadSetting()
        {
            if (siloConfig.implementationType == ImplementationType.SNAPPER)
            {
                if (siloConfig.benchmarkType == BenchmarkType.SMALLBANK || siloConfig.benchmarkType == BenchmarkType.NEWSMALLBANK)
                    workloadGroup = WorkloadParser.GetSnapperSmallBankWorkLoadFromXML(experimentID);
                else if (siloConfig.benchmarkType == BenchmarkType.TPCC)
                    workloadGroup = WorkloadParser.GetSnapperTPCCWorkLoadFromXML(experimentID);
            }
            else if (siloConfig.implementationType == ImplementationType.NONTXN)
            {
                if (siloConfig.benchmarkType == BenchmarkType.SMALLBANK)
                    workloadGroup = WorkloadParser.GetNTSmallBankWorkloadFromXML(experimentID);
                else if (siloConfig.benchmarkType == BenchmarkType.TPCC)
                    workloadGroup = WorkloadParser.GetNTTPCCWorkloadFromXML(experimentID);
            }  
            else if (siloConfig.implementationType == ImplementationType.ORLEANSTXN) 
                workloadGroup = WorkloadParser.GetOrleansWorkloadFromXML(experimentID);
        }

        static async void RunExperiments()
        {
            PrintResultHeader();

            foreach (var pactPercent in workloadGroup.pactPercent)
            {
                var workload = new Workload();
                workload.pactPercent = pactPercent;
                
                for (int i = 0; i < workloadGroup.txnSize.Length; i++)
                {
                    workload.txnSize = workloadGroup.txnSize[i];
                    workload.numWriter = workloadGroup.numWriter[i];

                    for (int k = 0; k < workloadGroup.noDeadlock.Length; k++)
                    {
                        workload.noDeadlock = workloadGroup.noDeadlock[k];

                        for (int j = 0; j < workloadGroup.distribution.Length; j++)
                        {
                            workload.distribution = workloadGroup.distribution[j];
                            workload.zipfianConstant = workloadGroup.zipfianConstant[j];
                            workload.actPipeSize = workloadGroup.actPipeSize[j];
                            workload.pactPipeSize = workloadGroup.pactPipeSize[j];

                            // generate transaction requests
                            var shared_requests = new SharedRequest();   // <epoch, <producerID, <isDet, grainIDs>>>
                            for (int epoch = 0; epoch < Constants.numEpoch; epoch++) shared_requests.Add(epoch, new Queue<Tuple<bool, RequestData>>());

                            var workloadGenerator = new WorkloadGenerator(siloConfig, workload);
                            switch (siloConfig.benchmarkType)
                            {
                                case BenchmarkType.SMALLBANK:
                                    workloadGenerator.GenerateSmallBankWorkload(shared_requests);
                                    break;
                                case BenchmarkType.NEWSMALLBANK:
                                    workloadGenerator.GenerateSmallBankWorkload(shared_requests);
                                    break;
                                case BenchmarkType.TPCC:
                                    workloadGenerator.GenerateTPCCWorkload(shared_requests);
                                    break;
                                default:
                                    throw new Exception($"Exception: Unknown benchmark");
                            }

                            // run the experiment under this specific setting
                            var singleExperimentManager = new SingleExperimentManager(experimentID, siloConfig, workload, shared_requests);
                            await singleExperimentManager.RunOneExperiment();
                        }
                    }
                }
            }
            experimentDone = true;
        }

        static void PrintResultHeader()
        {
            if (siloConfig.benchmarkType == BenchmarkType.NEWSMALLBANK) return;

            using (var file = new StreamWriter(Constants.resultPath, true))
            {
                file.Write("Fig    ");
                file.Write("Silo_vCPU   implementation  benchmark   loggingEnabled  NUM_OrderGrain_PER_D    ");
                file.Write("pactPercent txnSize numWriter   distribution    zipfianConstant actPipeSize pactPipeSize    noDeadlock  ");
                
                file.Write("pact_tp sd  act_tp  sd  total_abort_rate    ");
                file.Write("abortRWConflict abortDeadlock   abortNotSureSerializable    abortNotSerializable    ");
                file.Write("pact-50th-latency(ms)   pact-90th-latency(ms)   pact-99th-latency(ms)   ");
                file.Write("act-50th-latency(ms)    act-90th-latency(ms)    act-99th-latency(ms)");
                file.WriteLine();
            }
        }
    }
}