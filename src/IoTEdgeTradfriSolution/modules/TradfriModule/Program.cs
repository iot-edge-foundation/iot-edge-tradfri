namespace TradfriModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Tomidix.NetStandard.Tradfri;

    class Program
    {
        private const string DefaultAppSecret = "";

        private const string DefaultIpAddress = "";

        private const string DefaultGatewayName = "";

        private static string _moduleId; 

        private static TradfriController _controller;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, ioTHubModuleClient);

            await ioTHubModuleClient.OpenAsync();

            Console.WriteLine("      _                         ___      _____   ___     _");
            Console.WriteLine("     /_\\   ___ _  _  _ _  ___  |_ _| ___|_   _| | __| __| | __ _  ___  ");
            Console.WriteLine("    / _ \\ |_ /| || || '_|/ -_)  | | / _ \\ | |   | _| / _` |/ _` |/ -_)");
            Console.WriteLine("   /_/ \\_\\/__| \\_,_||_|  \\___| |___|\\___/ |_|   |___|\\__,_|\\__, |\\___|");
            Console.WriteLine("                                                           |___/");
            Console.WriteLine(" ");
            Console.WriteLine("   Copyright © 2019 - IoT Edge Foundation");
            Console.WriteLine(" ");

            _moduleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");

            Console.WriteLine($"Module '{_moduleId}' initialized");
            Console.WriteLine($".Net framework version '{Environment.GetEnvironmentVariable("DOTNET_VERSION")}' in use");

            // assign direct method handler again
            await ioTHubModuleClient.SetMethodHandlerAsync(
                "generateAppSecret",
                GenerateAppSecretMethodCallBack,
                ioTHubModuleClient);

            Console.WriteLine("Attached method handler: generateAppSecret");            
        }

        static async Task<MethodResponse> GenerateAppSecretMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            CloseController();

            Console.WriteLine("Executing GenerateAppSecretMethodCallBack");
            
            var messageBytes = methodRequest.Data;
            var messageJson = Encoding.UTF8.GetString(messageBytes);
            var command = (GenerateAppSecretCommand)JsonConvert.DeserializeObject(messageJson, typeof(GenerateAppSecretCommand));
            
            var tradfriAuth = _controller.GenerateAppSecret(command.gatewaySecret, _moduleId);

            var secretResponse = new GenerateAppSecretResponse{appSecret = tradfriAuth.PSK};

            var json = JsonConvert.SerializeObject(secretResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }

        private static string GatewayName { get; set; } = DefaultGatewayName;
        
        private static string AppSecret { get; set; } = DefaultAppSecret;

        private static string IpAddress { get; set; } = DefaultIpAddress;

        /// <summary>
        /// Call back function for updating the desired properties
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine("OnDesiredPropertiesUpdate started");

            var client = userContext as ModuleClient;

            if (desiredProperties == null)
            {
                Console.WriteLine("Empty desired properties ignored.");

                return;
            }

            try
            {
                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("gatewayName")) 
                {
                    if (desiredProperties["gatewayName"] != null)
                    {
                        GatewayName = desiredProperties["gatewayName"];
                    }
                    else
                    {
                        GatewayName = DefaultGatewayName;
                    }

                    Console.WriteLine($"GatewayName changed to {GatewayName}");

                    reportedProperties["gatewayName"] = GatewayName;
                } 
                else
                {
                    Console.WriteLine($"GatewayName ignored");
                }

                if (desiredProperties.Contains("appSecret")) 
                {
                    if (desiredProperties["appSecret"] != null)
                    {
                        AppSecret = desiredProperties["appSecret"];
                    }
                    else
                    {
                        AppSecret = DefaultAppSecret;
                    }

                    Console.WriteLine($"AppSecret changed to {AppSecret}");

                    reportedProperties["appSecret"] = AppSecret;
                }
                else
                {
                    Console.WriteLine($"AppSecret ignored");
                }

                if (desiredProperties.Contains("ipAddress")) 
                {
                    if (desiredProperties["ipAddress"] != null)
                    {
                        IpAddress = desiredProperties["ipAddress"];
                    }
                    else
                    {
                        IpAddress = DefaultIpAddress;
                    }

                    Console.WriteLine($"IpAddress changed to {IpAddress}");

                    reportedProperties["ipAddress"] = IpAddress;
                }
                else
                {
                    Console.WriteLine($"IpAddress ignored");
                }

                if (reportedProperties.Count > 0)
                {
                    await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }

                AttachController();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Desired properties change error: {ex}");
                
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine($"Error when receiving desired property: {exception}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when receiving desired property: {ex.Message}");
            }
        }

        private static void CloseController()
        {
            if (_controller == null)
            {
                Console.WriteLine($"Controller is disconnected");
                
                return;
            }

            Console.WriteLine($"Connecting to {_controller.GateWayName}");
            _controller = null;
        }

        private static void AttachController()
        {
            CloseController();

            if (!string.IsNullOrEmpty(AppSecret)
                    && !string.IsNullOrEmpty(GatewayName)
                    && !string.IsNullOrEmpty(IpAddress))
            {
                Console.WriteLine($"Connecting to {_controller.GateWayName}");

                var controller = new TradfriController(GatewayName, IpAddress);
                _controller.ConnectAppKey(AppSecret, _moduleId);

                Console.WriteLine($"Connected to {_controller.GateWayName}");
            }
            else
            {
                Console.WriteLine($"Connecting controller skipped due to incomplete parameters");
            }
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
//         static async Task<MessageResponse> PipeMessage(Message message, object userContext)
//         {
//             var moduleClient = userContext as ModuleClient;
//             if (moduleClient == null)
//             {
//                 throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
//             }

//             byte[] messageBytes = message.GetBytes();
//             string messageString = Encoding.UTF8.GetString(messageBytes);
// //            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

//             if (!string.IsNullOrEmpty(messageString))
//             {
//                 using (var pipeMessage = new Message(messageBytes))
//                 {
//                     foreach (var prop in message.Properties)
//                     {
//                         pipeMessage.Properties.Add(prop.Key, prop.Value);
//                     }
//                     await moduleClient.SendEventAsync("output1", pipeMessage);
                
//                     Console.WriteLine("Received message sent");
//                 }
//             }
//             return MessageResponse.Completed;
//         }
    }

    public class GenerateAppSecretCommand
    {
        public string gatewaySecret {get; set;}
    }

    public class GenerateAppSecretResponse
    {
        public string appSecret {get; set;}
    }
}
