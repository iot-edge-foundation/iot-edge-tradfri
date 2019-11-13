# iot-edge-tradfri

Azure IoT Edge support for IKEA Trådfri/Tradfri. The logic is limited to lights.

# IoT Edge

This logic is available as [Docker container](https://hub.docker.com/repository/docker/svelde/iot-edge-tradfri).

This Docker module is optimized for [Azure IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/).

```
docker pull svelde/iot-edge-tradfri:0.1.0-windows-amd64
docker pull svelde/iot-edge-tradfri:0.1.0-arm32v7
docker pull svelde/iot-edge-tradfri:0.1.0-amd64
```

*Note*: This module is tested using the amd64 version.

![How to use in routing flow](media/flow.png)

# Current limitations

At this moment, the module supports:

* Generating a private key for the module/application
* Connecting to Hub when right properties are filled in
* Reboot Hub / Reconnect to hub
* Overview of all rooms and its devices
* Set color/brightness/state of light
* Set color/brightness of group of lights

## Work in progress

What's coming:

* CollectInformation does not show current state of lights
* Events/changes are not shown
* Mood is not supported by groups

![Logging shown at start of module](media/logging.png)

# Usage

## 1. Initialization

Fill in the desired properties:

* gatewayName (required; choose a name)
* ipAddress (required; the IP address of the Trådfri hub)

Then call the "generateAppSecret" direct method. Pass the "gateway secret", found on the back of your Trådfri hub.

*Note*: the name of the module will be used as application name.

The returned "application secret" has to be filled in in the desired property. 

* appSecret

## 2. Controlling lights

Lights can be controlled individually or as a group. 

State, brightness and color can be set. Mood is not available yet.

# Interface

## Desired and reported properties

The following properties are used:

* gatewayName (required; choose a name)
* ipAddress (required; the IP address of the Trådfri hub)
* appSecret (required; generate this with appropriate Direct Method)

## Direct Methods

The following Direct Methods are offered:

* generateAppSecret

### generateAppSecret

The input is:

```
public class GenerateAppSecretRequest
{
  public string gatewaySecret {get; set;}
}
```

*Note*: The gateway secret can be found on the back of your Trådfri hub.

The output is :

```
public class GenerateAppSecretResponse
{
  public string appSecret {get; set;}
  public string errorMessage { get; set; }
}
```

Fill in this appSecret in the related Desired Property.

## collectInformation

The input format is empty:

```
{}
```

The output format is:

```
public class CollectInformationResponse : CollectedInformation
{
  public int responseState { get; set; }
}

public class CollectedInformation
{
  public CollectedInformation()
  {
    groups = new List<Group>();
  }      

  public List<Group> groups {get; private set;}
}

public class Group
{
  public long id { get; set; }

  public string name { get; set; }
  public long lightState { get; set; }

  public long activeMood {get; set;}

  public List<Device> devices {get; private set;}

  public Group()
  {
    devices = new List<Device>();
  }
}

public class Device
{
  public long id { get; set; }

  public string deviceType { get; set; }

  public string deviceTypeExt { get; set; }

  public string name { get; set; }

  public long battery { get; set; }

  public DateTime lastSeen { get; set; }

  public string reachableState { get; set; }

  public long dimmer { get; set; }

  public string state { get; set; }

  public string colorHex { get; set; }
}
```

### Example

This is an example of the response:

```
{
	"status": 200,
	"payload": {
		"responseState": 0,
		"groups": [{
				"id": 131090,
				"name": "Room 1",
				"lightState": 0,
				"activeMood": 196659,
				"devices": [{
						"id": 65593,
						"deviceType": "Remote",
						"deviceTypeExt": "TRADFRI remote control",
						"name": "TRADFRI remote control 1",
						"battery": 34,
						"lastSeen": "2019-11-12T21:33:32Z",
						"reachableState": "1",
						"dimmer": -1,
						"state": "",
						"colorHex": null
					}, {
						"id": 65594,
						"deviceType": "Light",
						"deviceTypeExt": "TRADFRI bulb GU10 WS 400lm",
						"name": "TRADFRI bulb 42",
						"battery": 0,
						"lastSeen": "2019-11-12T19:58:08Z",
						"reachableState": "0",
						"dimmer": 220,
						"state": "f1e0b5",
						"colorHex": null
					}
				]
			}
		]
	}
}

```

## reboot

The input is empty:

```
{}
```

The output is:

```
public class RebootResponse
{
  public int responseState { get; set; }
  public string errorMessage { get; set; }
}
```

*Note*: After this method is sent, the Hub is actually rebooting. This takes some time. You have to reconnect later before you can continue to work with the hub.

## Reconnect

The input is empty:

```
{}
```

The output is:

```
public class ReconnectResponse
{
  public int responseState { get; set; }
  public string errorMessage { get; set; }
}
```

## SetLight

The input is empty:

```
public class SetLightRequest
{
  public long id { get; set; }
  public bool? turnLightOn { get; set; }
  public string color { get; set; }
  public int? brightness { get; set; }
}
```

The output is:

```
public class SetLightResponse
{
  public int responseState { get; set; }
  public string errorMessage { get; set; }
}
```

## SetGroup

The input is empty:

```
public class SetLightRequest
{
  public long id { get; set; }
  public bool? turnLightOn { get; set; }
  public int? brightness { get; set; }
}
```

The output is:

```
public class SetGroupResponse
{
  public int responseState { get; set; }
  public string errorMessage { get; set; }
}
```

# Aknowledgement

The logic in this module is based on https://github.com/tomidix/CSharpTradFriLibrary.

# Disclaimer

This module is trying to honor all rights of Ikea regarding Trådfri.
