namespace TradfriModule
{
    using System;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Tomidix.NetStandard.Tradfri;
    using Tomidix.NetStandard.Tradfri.Models;
    using System.Linq;

    class Program
    {
        private const string DefaultAppSecret = "";

        private const string DefaultIpAddress = "";

        private const string DefaultGatewayName = "";

        private static string _moduleId; 

        private static TradfriController _controller;

        private static CollectedInformation _collectedInformation;

        private static ModuleClient _ioTHubModuleClient;

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
            _moduleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");

            Console.WriteLine("      _                         ___      _____   ___     _");
            Console.WriteLine("     /_\\   ___ _  _  _ _  ___  |_ _| ___|_   _| | __| __| | __ _  ___  ");
            Console.WriteLine("    / _ \\ |_ /| || || '_|/ -_)  | | / _ \\ | |   | _| / _` |/ _` |/ -_)");
            Console.WriteLine("   /_/ \\_\\/__| \\_,_||_|  \\___| |___|\\___/ |_|   |___|\\__,_|\\__, |\\___|");
            Console.WriteLine("                                                           |___/");
            Console.WriteLine("    _____            _  __     _  ");
            Console.WriteLine("   |_   _| _ () _ __| |/ _|_ _(_) ");
            Console.WriteLine("     | || '_/ _` / _` |  _| '_| | ");
            Console.WriteLine("     |_||_| \\__,_\\__,_|_| |_| |_| ");
            Console.WriteLine(" ");
            Console.WriteLine("   Copyright © 2019 - IoT Edge Foundation");
            Console.WriteLine(" ");

            Console.WriteLine($".Net framework version '{Environment.GetEnvironmentVariable("DOTNET_VERSION")}' in use");

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Execute callback method for Twin desired properties updates
            var twin = await _ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, _ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, _ioTHubModuleClient);

            await _ioTHubModuleClient.OpenAsync();

            Console.WriteLine($"Module '{_moduleId}' initialized");

            Console.WriteLine("Attached routing output: output1"); 

            //// assign direct method handlers 

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "generateAppSecret",
                GenerateAppSecretMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: generateAppSecret");            

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "collectInformation",
                collectInformationMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: collectInformation");            

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "reboot",
                RebootMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: reboot");    

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "reconnect",
                ReconnectMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: reconnect");  

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "setLight",
                SetLightMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: setLight");    

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "setGroup",
                SetGroupMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: setGroup");  
        }

        static async Task<MethodResponse> ReconnectMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing ReconnectMethodCallBack");

            var reconnectResponse = new ReconnectResponse{responseState = 0};

            try
            {
              Console.WriteLine("Reconnecting...");

              await AttachController();

              Console.WriteLine("Reconnected");
            }
            catch (Exception ex)
            {
               reconnectResponse.errorMessage = ex.Message;   
            }
            
            var json = JsonConvert.SerializeObject(reconnectResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
        }                

        static async Task<MethodResponse> SetGroupMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing SetGroupMethodCallBack");

            var setGroupResponse = new SetGroupResponse{responseState = 0};

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var request = JsonConvert.DeserializeObject<SetGroupRequest>(messageJson);

                if (_controller == null)
                {
                    setGroupResponse.responseState = -1;
                }
                else
                {
                    var group = await _controller.GroupController.GetTradfriGroup(request.id);

                    if (group == null)
                    {
                        setGroupResponse.responseState = -2;
                    }
                    else
                    {
                        // TODO Mood

                        // Brightness

                        if (request.brightness.HasValue 
                                && request.brightness.Value >= 0
                                && request.brightness.Value <= 10)
                        {
                            await _controller.GroupController.SetDimmer(group, request.brightness.Value * 10 * 254 / 100);

                            Console.WriteLine($"Group '{request.id}' brightness set to '{request.brightness.Value}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored brightness for '{request.id}'");
                        }

                        // On/Off

                        if (request.turnLightOn.HasValue)
                        {
                            await _controller.GroupController.SetLight(group, request.turnLightOn.Value);

                            Console.WriteLine($"Group '{request.id}' set to '{request.turnLightOn.Value}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored turnLightOn for '{request.id}'");
                        }
                    }               
                }
            }
            catch (Exception ex)
            {
               setGroupResponse.errorMessage = ex.Message;   
            }
            
            var json = JsonConvert.SerializeObject(setGroupResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
        }

        static async Task<MethodResponse> SetLightMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing SetLightMethodCallBack");

            var setLightResponse = new SetLightResponse{responseState = 0};

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var request = JsonConvert.DeserializeObject<SetLightRequest>(messageJson);

                if (_controller == null)
                {
                    setLightResponse.responseState = -1;
                }
                else
                {
                    var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                    var device = deviceObjects.FirstOrDefault(x => x.DeviceType == DeviceType.Light
                                                            && x.ID == request.id);

                    if (device == null)
                    {
                        setLightResponse.responseState = -3;
                    }
                    else
                    {
                        // Color

                        var color = GetPredefinedColor(request.color);

                        if (!string.IsNullOrEmpty(color))
                        {
                            await _controller.DeviceController.SetColor(device, color);

                            Console.WriteLine($"Light '{request.id}' color set to '{color}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored color for '{request.id}'");
                        }

                        // Brightness

                        if (request.brightness.HasValue 
                                && request.brightness.Value >= 0
                                && request.brightness.Value <= 10)
                        {
                            await _controller.DeviceController.SetDimmer(device, request.brightness.Value * 10 * 254 / 100);

                            Console.WriteLine($"Light '{request.id}' brightness set to '{request.brightness.Value}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored brightness for '{request.id}'");
                        }

                        // On/Off

                        if (request.turnLightOn.HasValue)
                        {
                            await _controller.DeviceController.SetLight(device, request.turnLightOn.Value);

                            Console.WriteLine($"Light '{request.id}' set to '{request.turnLightOn.Value}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored turnLightOn for '{request.id}'");
                        }
                    }               
                }
            }
            catch (Exception ex)
            {
               setLightResponse.errorMessage = ex.Message;   
            }
            
            var json = JsonConvert.SerializeObject(setLightResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
        }

        static async Task<MethodResponse> RebootMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing RebootMethodCallBack");

            var rebootResponse = new RebootResponse{responseState = 0};

            try
            {
                if (_controller == null)
                {
                    rebootResponse.responseState = -1;
                }
                else
                {
                    await _controller.GatewayController.Reboot();
                    CloseController();
                }
            }
            catch (Exception ex)
            {
               rebootResponse.errorMessage = ex.Message;   
            }

            var json = JsonConvert.SerializeObject(rebootResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
        }

        static async Task<MethodResponse> GenerateAppSecretMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing GenerateAppSecretMethodCallBack");

            var secretResponse = new GenerateAppSecretResponse();

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var command = (GenerateAppSecretRequest)JsonConvert.DeserializeObject(messageJson, typeof(GenerateAppSecretRequest));
                
                // Never expose command.gatewaySecret !

                ConstructController();

                var tradfriAuth = _controller.GenerateAppSecret(command.gatewaySecret, _moduleId);

                Console.WriteLine($"Secret generated of '{tradfriAuth?.PSK?.Length}' characters long.");

                secretResponse.appSecret = tradfriAuth.PSK;
            }
            catch (Exception ex)
            {
               secretResponse.errorMessage = ex.Message;   
            }

            var json = JsonConvert.SerializeObject(secretResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }
        
        static async Task<MethodResponse> collectInformationMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            var collectInformationResponse = new CollectInformationResponse{ responseState = 0 };

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var command = JsonConvert.DeserializeObject<CollectInformationRequest>(messageJson);

                var filter = command.filter ?? string.Empty;

                Console.WriteLine($"Executing collectInformationMethodCallBack: Filter: '{filter}'");

                if (_controller == null)
                {
                    collectInformationResponse.responseState = -1;
                }
                else
                {
                    var groups = await _controller.GatewayController.GetGroupObjects();

                    if ( groups == null)
                    {
                        collectInformationResponse.responseState = -3;
                    }
                    else
                    {
                        var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                        if ( deviceObjects == null)
                        {
                            collectInformationResponse.responseState = -4;
                        }
                        else
                        {
                            var collected = await CollectInformation();

                            if (!collected)
                            {
                                collectInformationResponse.responseState = -5;
                            }
                            else
                            {
                                foreach(var group in _collectedInformation.groups)
                                {
                                    if (string.IsNullOrEmpty(filter)
                                            || filter.Contains(group.Key))
                                    {
                                        collectInformationResponse.groups.Add(group.Key, group.Value);                                
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
               collectInformationResponse.errorMessage = ex.Message;   
            }            

            var json = JsonConvert.SerializeObject(collectInformationResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }

        static async Task<bool> CollectInformation()
        {
            Console.WriteLine("Information collecting (this can take a while...)");

            var result = false;

            if (_controller != null)
            {
                var groups = await _controller.GatewayController.GetGroupObjects();

                Console.WriteLine($"Number of groups found: '{groups.Count}'");

                var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                Console.WriteLine($"Number of devices found: '{deviceObjects.Count}'");

                if ( groups != null
                        && deviceObjects != null)
                {
                    _collectedInformation = new CollectedInformation();

                    foreach (var group in groups)
                    {
                        var deviceGroup = new Group
                        {
                            name = group.Name, 
                            lightState = group.LightState, 
                            activeMood = group.ActiveMood
                        };

                        Console.WriteLine($"{group.ID} - {group.Name} - {group.ActiveMood}");

                        foreach (var id in group.Devices.The15002.ID)
                        {
                            var device = new Device();

                            var deviceObject = deviceObjects.FirstOrDefault(x => x.ID == id);

                            if (deviceObject != null)
                            {
                                // Add observer for each device to route changes.
                                _controller.DeviceController.
                                    ObserveDevice(deviceObject, async d => await NotifyChange(d));

                                device.deviceType = deviceObject.DeviceType.ToString();
                                device.name = deviceObject.Name;
                                device.battery = deviceObject.Info.Battery;
                                device.deviceTypeExt = deviceObject.Info.DeviceType.ToString();
                                device.lastSeen = deviceObject.LastSeen;
                                device.reachableState = deviceObject.ReachableState.ToString();
                                device.serial = deviceObject.Info.Serial;
                                device.firmwareVersion = deviceObject.Info.FirmwareVersion;
                                device.powerSource = deviceObject.Info.PowerSource.ToString();

                                if (deviceObject.LightControl != null 
                                        && deviceObject.LightControl.Count > 0)
                                {
                                    device.dimmer = deviceObject.LightControl[0].Dimmer;
                                    device.state = deviceObject.LightControl[0].State.ToString();
                                    device.colorHex = deviceObject.LightControl[0].ColorHex;
                                }
                            }

                            deviceGroup.devices.Add(id.ToString(), device);
                        }

                        _collectedInformation.groups.Add(group.ID.ToString(), deviceGroup);                                    
                    }

                    result = true;
                }
             
            }

            Console.WriteLine("Information collected");

            return result;
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

                    Console.WriteLine($"GatewayName changed to '{GatewayName}'");

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

                    Console.WriteLine($"AppSecret changed to '{AppSecret}'");

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

                    Console.WriteLine($"IpAddress changed to '{IpAddress}'");

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

                await AttachController();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Desired properties change error: {ex}");
                
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine($"Error when receiving desired properties: {exception}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when receiving desired properties: {ex.Message}");
            }
        }

        private static void CloseController()
        {
            if (_controller == null)
            {
                Console.WriteLine($"Controller is already closed");
                
                return;
            }

            try
            {
                var name = _controller.GateWayName;

                Console.WriteLine($"Closing '{name}'");
                
                _collectedInformation = null;

                _controller = null;

                Console.WriteLine($"Closed '{name}'");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"CloseController exception {ex.Message}");
            }
        }

        /// <summary>
        /// Construct controller before first key generation.
        /// </summary>
        private static void ConstructController()
        {
            CloseController();

            if (!string.IsNullOrEmpty(GatewayName)
                    && !string.IsNullOrEmpty(IpAddress))
            {
                try
                {
                    Console.WriteLine($"Construct '{GatewayName}' at '{IpAddress}'");

                    _controller = new TradfriController(GatewayName, IpAddress);

                    Console.WriteLine($"ontroller constructed");
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"ConstructController exception {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Constructing controller skipped due to incomplete parameters");
            }
        }

        /// <summary>
        /// Attach controller to collect information.
        /// </summary>
        private static async Task AttachController()
        {
            try
            {
                CloseController();

                if (!string.IsNullOrEmpty(AppSecret)
                        && !string.IsNullOrEmpty(GatewayName)
                        && !string.IsNullOrEmpty(_moduleId)
                        && !string.IsNullOrEmpty(IpAddress))
                {
                    Console.WriteLine($"Connecting to '{GatewayName}' at '{IpAddress}'");

                    _controller = new TradfriController(GatewayName, IpAddress);

                    Console.WriteLine($"Controller created");

                    _controller.ConnectAppKey(AppSecret, _moduleId);

                    Console.WriteLine($"Connected to '{GatewayName}'");

                    var collected = await CollectInformation();

                    if (collected)
                    {
                        Console.WriteLine($"Constructed '{GatewayName}'");
                    }
                    else
                    {
                        Console.WriteLine($"Connecting controller skipped due to failed collection");
                    }
                }
                else
                {
                    Console.WriteLine($"Connecting controller skipped due to incomplete parameters");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Connecting '{GatewayName}/{_moduleId}' failed due to '{ex.Message}'");

                throw;
            }
        }

        private static string GetPredefinedColor(string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                return null;
            }

            var fields = typeof(TradfriColors).GetFields();

            var field = fields.FirstOrDefault(x => x.Name == color);

            return field != null ? (string)field.GetValue(null) : string.Empty;
        }

        private static async Task NotifyChange(TradfriDevice device)
        {
            if (device == null)
            {
                Console.WriteLine($"Device is empty");
                return;
            }

            Console.WriteLine($"Change detected on device '{device.Name}'");

            var routedMessage = new RoutedMessage
            {
                id = device.ID,
                name = device.Name,
            }; 

            if (device.LightControl != null
                    && device.LightControl.Count > 0)
            {
                routedMessage.state = device.LightControl[0].State.ToString();
                routedMessage.brightness = device.LightControl[0].Dimmer;
                routedMessage.color = device.LightControl[0].ColorHex;   
            }

            var json = JsonConvert.SerializeObject(routedMessage);

            if (!string.IsNullOrEmpty(json))
            {
                using (var pipeMessage = new Message(Encoding.UTF8.GetBytes(json)))
                {
                    await _ioTHubModuleClient.SendEventAsync("output1", pipeMessage);
                }
            }
        } 
    }
}
