# iot-edge-tradfri

Azure IoT Edge support for IKEA Trådfri/Tradfri. The logic is limited to lights.

# Usage

## 1. Initialization

## 2. Controlling lights

# Interface

## Desired and reported properties

The following properties are used:

* gatewayName
* ipAddress
* appSecret 

*Note:* appSecret should be set using the appropriate Direct Method

## Direct Methods

The following Direct Methods are offered:

* generateAppSecret

### generateAppSecret

The input is:

```
public class GenerateAppSecretCommand
{
  public string gatewaySecret {get; set;}
}
```

The output is :

```
public class GenerateAppSecretResponse
{
  public string appSecret {get; set;}
}
```

# Aknowledgement

The logic in this module is based on https://github.com/tomidix/CSharpTradFriLibrary

# Disclaimer

This module is trying to honor all rights of Ikea regarding Trådfri.  
