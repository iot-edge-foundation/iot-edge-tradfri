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
    using System.Collections.Generic;

    class Program
    {
        private const int DefaultInterval = -1;

        private const string DefaultAppSecret = "";

        private const string DefaultIpAddress = "";

        private const string DefaultGatewayName = "";

        private const bool DefaultUseExtendedApplicationName = false;

        private const bool DefaultAllowHexColors = false;

        private static string _moduleId; 

        private static string _deviceId;

        private static TradfriController _controller;

        private static CollectedInformation _collectedInformation;

        private static ModuleClient _ioTHubModuleClient;

        private static DateTime _lastObserveDevices = DateTime.MinValue;

        public static bool _attachingInProgress = true;

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
            _deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
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
            Console.WriteLine("   Copyright © 2019-2020 - IoT Edge Foundation");
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

            Console.WriteLine($"Module '{_deviceId}'-'{_moduleId}' initialized");

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
                "setOutlet",
                SetOutletMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: setOutlet");    

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "setGroup",
                SetGroupMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: setGroup");  

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "getGatewayInfo",
                GetGatewayInfoMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: getGatewayInfo");  

            await _ioTHubModuleClient.SetMethodHandlerAsync(
                "collectBatteryPower",
                CollectBatteryPowerMethodCallBack,
                _ioTHubModuleClient);

            Console.WriteLine("Attached method handler: collectBatteryPower");  

            var thread = new Thread(() => ThreadBody(_ioTHubModuleClient));
            thread.Start();
        }

        private static async void ThreadBody(object userContext)
        {
            var client = userContext as ModuleClient;

            if (client == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            while (true)
            {
                if (!_attachingInProgress)
                {
                    if (Interval <= 0)
                    {
                        // we only want to start observing once

                        if (_lastObserveDevices != DateTime.MaxValue)
                        {
                            Console.WriteLine("Observing devices only once...");

                            await ObserveDevices();

                            _lastObserveDevices = DateTime.MaxValue;
                        }
                    }
                    else
                    {
                        // we only want to restart observing once every [Interval] minutes

                        var now = DateTime.Now;

                        if (now > _lastObserveDevices.AddMinutes(Interval) )

                        Console.WriteLine("Observing devices triggered...");

                        await ObserveDevices();

                        _lastObserveDevices = now;
                    }
                }
                else
                {
                    Console.WriteLine("Observing devices delayed due to busy attaching controller");
                }

                Thread.Sleep(10000);
            }
        }

       static async Task<MethodResponse> GetGatewayInfoMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing GetGatewayInfoMethodCallBack");

            var gatewayInfoResponse = new GatewayInfoResponse{responseState = 0};

            try
            {
                Console.WriteLine("Getting gateway info...");

                if (_controller == null
                        || _controller.GatewayController == null)
                {
                    gatewayInfoResponse.responseState = -1;
                }
                else
                {
                    var gatewayInfo = await _controller.GatewayController.GetGatewayInfo();

                    if (gatewayInfo == null)
                    {
                        gatewayInfoResponse.responseState = -2;
                    }
                    else
                    {
                        gatewayInfoResponse.commissioningMode = gatewayInfo.CommissioningMode;
                        gatewayInfoResponse.currentTimeISO8601 = gatewayInfo.CurrentTimeISO8601;
                        gatewayInfoResponse.firmware = gatewayInfo.Firmware;
                        gatewayInfoResponse.gatewayID = gatewayInfo.GatewayID;
                        gatewayInfoResponse.gatewayTimeSource = gatewayInfo.GatewayTimeSource;
                        gatewayInfoResponse.gatewayUpdateProgress = gatewayInfo.GatewayUpdateProgress;
                        gatewayInfoResponse.homekitID = gatewayInfo.HomekitID;
                        gatewayInfoResponse.ntp = gatewayInfo.NTP;
                        gatewayInfoResponse.otaType = gatewayInfo.OtaType;
                        gatewayInfoResponse.OtaUpdateState = gatewayInfo.OtaUpdateState;
                    }
                }

                Console.WriteLine("Info collected.");
            }
            catch (Exception ex)
            {
                gatewayInfoResponse.errorMessage = ex.Message;   
                gatewayInfoResponse.responseState = -999;
            }
            
            var json = JsonConvert.SerializeObject(gatewayInfoResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
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
                setGroupResponse.responseState = -999;
            }
            
            var json = JsonConvert.SerializeObject(setGroupResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            return response;
        }


        static async Task<MethodResponse> SetOutletMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            Console.WriteLine("Executing SetOutletMethodCallBack");

            var setOutletResponse = new SetOutletResponse{responseState = 0};

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var request = JsonConvert.DeserializeObject<SetOutletRequest>(messageJson);

                if (_controller == null)
                {
                    setOutletResponse.responseState = -1;
                }
                else
                {
                    var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                    var device = deviceObjects.FirstOrDefault(x => x.DeviceType == DeviceType.ControlOutlet
                                                                        && x.ID == request.id);

                    if (device == null)
                    {
                        setOutletResponse.responseState = -2;
                    }
                    else
                    {
                        // Outlet State

                        if (request.state.HasValue)
                        {
                            var state = request.state.Value;

                            await _controller.DeviceController.SetOutlet(device, state);

                            Console.WriteLine($"Outlet '{request.id}' set to '{state}'");
                        }
                        else
                        {
                            Console.WriteLine($"Ignored outlet state for '{request.id}'");
                        }
                    }               
                }
            }
            catch (Exception ex)
            {
                setOutletResponse.errorMessage = ex.Message;  
                setOutletResponse.responseState = -999;
            }
            
            var json = JsonConvert.SerializeObject(setOutletResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            Console.WriteLine("Executed SetOutletMethodCallBack");

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
                        setLightResponse.responseState = -2;
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
                setLightResponse.responseState = -999;
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
               rebootResponse.responseState = -999;
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

                var applicationName = UseExtendedApplicationName 
                                        ? _deviceId + _moduleId 
                                        : _moduleId;

                var tradfriAuth = _controller.GenerateAppSecret(command.gatewaySecret, applicationName);

                Console.WriteLine($"Secret for application '{applicationName}' generated of '{tradfriAuth?.PSK?.Length}' characters long. See method response.");

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
                        collectInformationResponse.responseState = -2;
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
                                            || filter.Contains(group.Key.ToString()))
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
               collectInformationResponse.responseState = -999;
            }            

            var json = JsonConvert.SerializeObject(collectInformationResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }



static async Task<MethodResponse> CollectBatteryPowerMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
            var collectBatteryPowerResponse = new CollectBatteryPowerResponse{ responseState = 0 };

            try
            {
                var messageBytes = methodRequest.Data;
                var messageJson = Encoding.UTF8.GetString(messageBytes);
                var command = JsonConvert.DeserializeObject<CollectBatteryPowerRequest>(messageJson);

                var all = command.all.HasValue ? command.all.Value : true;

                Console.WriteLine($"Executing collectBatteryPowerMethodCallBack: All: '{all}'");

                if (_controller == null)
                {
                    collectBatteryPowerResponse.responseState = -1;
                }
                else
                {
                    var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                    if ( deviceObjects == null)
                    {
                        collectBatteryPowerResponse.responseState = -4;
                    }
                    else
                    {
                        var collected = await CollectBatteryPower(all);

                        collectBatteryPowerResponse.devices = collected.OrderBy(x=>x.battery).ToArray();
                    }
                    
                }
            }
            catch (Exception ex)
            {
               collectBatteryPowerResponse.errorMessage = ex.Message;   
               collectBatteryPowerResponse.responseState = -999;
            }            

            var json = JsonConvert.SerializeObject(collectBatteryPowerResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }


        /// <summary>
        /// Collect hub information and make it available globally.
        /// </summary>
        static async Task<bool> CollectInformation()
        {
            Console.WriteLine("Information collecting (this can take a while...)");

            var result = false;

            if (_controller != null)
            {
                if (_controller.GatewayController == null)
                {
                    System.Console.WriteLine("controller.GatewayController is null.");
                    return false;
                }

                System.Console.WriteLine("Start GetGroupObjects");

                var groups = await _controller.GatewayController.GetGroupObjects();

                System.Console.WriteLine("Start GetDeviceObjects");

                var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                if ( groups != null
                        && deviceObjects != null)
                {
                    Console.WriteLine($"Number of groups found: '{groups.Count}'");
                    Console.WriteLine($"Number of devices found: '{deviceObjects.Count}'");

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

                            deviceGroup.devices.Add(id, device);
                        }

                        _collectedInformation.groups.Add(group.ID, deviceGroup);                                    
                    }

                    result = true;
                }
                else
                {
                    Console.WriteLine("No groups or devices found.");

                    return false;
                }
            }
            else
            {
                Console.WriteLine("Controller is null.");

                return false;
            }

            Console.WriteLine("Information collected");

            return result;
        }


        /// <summary>
        /// Collect battery power information.
        /// </summary>
        static async Task<List<BatteryPowerDevice>> CollectBatteryPower(bool all)
        {
            Console.WriteLine("Battery power information collecting (this can take a while...)");

            var result = new List<BatteryPowerDevice>();

            if (_controller != null
                    &&  _controller.GatewayController != null)
            {
                var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                if (deviceObjects != null)
                {
                    Console.WriteLine($"Number of devices found: '{deviceObjects.Count}'");
                    
                    foreach (var deviceObject in deviceObjects)
                    {
                        if (deviceObject.Info.PowerSource == PowerSource.InternalBattery
                                && !all)
                        {
                            Console.WriteLine($"Skip: '{deviceObject.Name}'");

                            continue;    
                        }

                        var device = new BatteryPowerDevice();          
                        device.deviceTypeExt = deviceObject.Info.DeviceType.ToString();
                        device.name = deviceObject.Name;
                        device.battery = deviceObject.Info.Battery;
                        device.powerSource = deviceObject.Info.PowerSource.ToString();

                        result.Add(device);                                    
                    }
                }
                else
                {
                    Console.WriteLine("No groups or devices found.");
                }           
            }
            else
            {
                System.Console.WriteLine("Controller or GatewayController not available.");
            }

            Console.WriteLine("Battery power information collected");

            return result;
        }

        private static string GatewayName { get; set; } = DefaultGatewayName;
        
        private static string AppSecret { get; set; } = DefaultAppSecret;

        private static string IpAddress { get; set; } = DefaultIpAddress;

        private static int Interval { get; set; } = DefaultInterval;

        private static bool UseExtendedApplicationName {get; set;} = DefaultUseExtendedApplicationName;

        private static bool AllowHexColors {get; set;} = DefaultAllowHexColors;

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

                if (desiredProperties.Contains("useExtendedApplicationName")) 
                {
                    if (desiredProperties["useExtendedApplicationName"] != null)
                    {
                        UseExtendedApplicationName = Convert.ToBoolean(desiredProperties["useExtendedApplicationName"]);
                    }
                    else
                    {
                        UseExtendedApplicationName = DefaultUseExtendedApplicationName;
                    }

                    Console.WriteLine($"UseExtendedApplicationName changed to '{UseExtendedApplicationName}'");

                    reportedProperties["useExtendedApplicationName"] = UseExtendedApplicationName;
                } 
                else
                {
                    Console.WriteLine($"UseExtendedApplicationName ignored");
                }

                if (desiredProperties.Contains("allowHexColors")) 
                {
                    if (desiredProperties["allowHexColors"] != null)
                    {
                        AllowHexColors = Convert.ToBoolean(desiredProperties["allowHexColors"]);
                    }
                    else
                    {
                        AllowHexColors = DefaultAllowHexColors;
                    }

                    Console.WriteLine($"AllowHexColors changed to '{AllowHexColors}'");

                    reportedProperties["AllowHexColors"] = AllowHexColors;
                } 
                else
                {
                    Console.WriteLine($"AllowHexColors ignored");
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

                    Console.WriteLine($"AppSecret changed to '[Not Exposed]'");

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

                if (desiredProperties.Contains("interval")) 
                {
                    if (desiredProperties["interval"] != null)
                    {
                        Interval = desiredProperties["interval"];
                    }
                    else
                    {
                        Interval = DefaultInterval;
                    }

                    Console.WriteLine($"Interval changed to '{Interval}'");

                    reportedProperties["interval"] = Interval;
                }
                else
                {
                    Console.WriteLine($"Interval ignored");
                }

                if (reportedProperties.Count > 0)
                {
                    await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }

                await AttachController();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Desired properties change error: {ex.Message}");
                
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
        /// Application key is already generated and registered.
        /// </summary>
        private static async Task AttachController()
        {
            _attachingInProgress = true;
            try
            {
                CloseController();

                if (!string.IsNullOrEmpty(AppSecret)
                        && !string.IsNullOrEmpty(GatewayName)
                        && !string.IsNullOrEmpty(_deviceId)
                        && !string.IsNullOrEmpty(_moduleId)
                        && !string.IsNullOrEmpty(IpAddress))
                {
                    Console.WriteLine($"Connecting to '{GatewayName}' at '{IpAddress}'");

                    _controller = new TradfriController(GatewayName, IpAddress);

                    Console.WriteLine($"Controller created");

                    var applicationName = UseExtendedApplicationName 
                                            ? _deviceId + _moduleId 
                                            : _moduleId;

                    _controller.ConnectAppKey(AppSecret, applicationName);

                    Console.WriteLine($"Connected application '{applicationName}'");

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
                    Console.WriteLine($"Connecting controller skipped due to incomplete parameters. Please execute 'generateAppSecret'.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Connecting '{GatewayName}/{_deviceId}/{_moduleId}' failed due to '{ex.Message}'");

                throw;
            }
            finally
            {
                _attachingInProgress = false;
            }

            
        }

        private static async Task ObserveDevices()
        {
            if (_controller != null
                    && _controller.GatewayController != null)
            {
                Console.WriteLine("Observe devices...");
                try
                {
                    var deviceObjects = await _controller.GatewayController.GetDeviceObjects();

                    if (deviceObjects == null)
                    {
                        Console.WriteLine("No observable devices available");

                        return;
                    }

                    Console.WriteLine($"Observe '{deviceObjects.Count}' devices.");

                    foreach(var deviceObject in deviceObjects)
                    {
                        if (deviceObject == null)
                        {
                            Console.WriteLine("Ignored null device...");
                            continue;
                        }
                        // Add observer for each device to route changes.
                        _controller.DeviceController.ObserveDevice(deviceObject, async d => await NotifyChange(d));
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Base logic for observing devices (for tradfri telemetry) failed ({ex})");
                }
            }
            else
            {
                Console.WriteLine($"Observing devices unavailable at this moment.");
            }
        }

        private static string GetPredefinedColor(string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                return null;
            }

            if (AllowHexColors)
            {
                return color;
            }

            var fields = typeof(TradfriColors).GetFields();

            var field = fields.FirstOrDefault(x => x.Name == color);

            return field != null ? (string)field.GetValue(null) : string.Empty;
        }

        private static async Task NotifyChange(TradfriDevice device)
        {
            if (_collectedInformation == null
                    || _collectedInformation.groups == null)
            {
                Console.WriteLine($"Notify ignored. Collection is empty");
                return;
            }

            if (device == null)
            {
                Console.WriteLine($"Notify ignored. Device is empty");
                return;
            }

            Console.WriteLine($"{DateTime.UtcNow} - Change detected on device '{device.Name}'");

            var routedMessage = new RoutedMessage
            {
                id = device.ID,
                name = device.Name,
                lastSeen = device.LastSeen,
            }; 

            if (device.LightControl != null
                    && device.LightControl.Count > 0)
            {
                routedMessage.state = device.LightControl[0].State.ToString();
                routedMessage.brightness = device.LightControl[0].Dimmer;
                routedMessage.colorHex = device.LightControl[0].ColorHex;   
            }

            if (device.Control != null
                    && device.Control.Count > 0)
            {
                routedMessage.state = device.Control[0].State.ToString();
                routedMessage.brightness = device.Control[0].Dimmer;
            }

            var group = _collectedInformation.groups.FirstOrDefault(x=> x.Value != null
                                                                && x.Value.devices != null
                                                                && x.Value.devices.ContainsKey(device.ID));
            if (group.Value != null)
            {
                routedMessage.groupId = group.Key;
                routedMessage.groupName = group.Value.name;
            }
        
            var json = JsonConvert.SerializeObject(routedMessage);

            if (!string.IsNullOrEmpty(json))
            {
                using (var pipeMessage = new Message(Encoding.UTF8.GetBytes(json)))
                {
                    pipeMessage.ContentType = "application/json";
                    pipeMessage.ContentEncoding = "utf-8";
                    
                    await _ioTHubModuleClient.SendEventAsync("output1", pipeMessage);
                }
            }
        } 
    }
}
