# BigClown Core - RPI - Azure Connection
- You need to have BigClown Core unit and connect it to Raspberry PI via USB
- upload [firmware](https://github.com/bigclownlabs/bcp-wireless-circus/releases/tag/v1.0.0)  to core unit and/or remote unit
- remote unit sends data via wireless conection to base unit and core unit writes data to serial port via USB-ACM
- use Raspberry PI 2/3 with Win 10 IoT Core and this project
- this project reads data from serial port and sends it into Azure IoT Hub
- you need Microsoft Azure subscription
- configure Azure IoT Hub and create device there, get device key/name and fill it in code
- consume from IoT Hub with stream analytics

![setup](https://github.com/PiDiBi/BigClown-RPI-Azure/raw/master/setup.jpg)

## Sample Data
Data from serial line:
```
["base/thermometer/i2c0-49", {"temperature": [25.25, "\u2103"]}]
["base/light/-", {"state": true}]
["remote/push-button/-", {"event-count": 16}]
```
Coressponding data to send to azure after parsing in RPI
```
{"address":"base","name":"thermometer","id":"i2c0-49","data":{"temperature":[25.25,"℃"]}}
{"address":"base","name":"light","id":"-","data":{"state":true}}
{"address":"remote","name":"push-button","id":"-","data":{"event-count":16}}
```
## Setup Device in Azure IoTHub
![IoT Hub Devices](https://github.com/PiDiBi/BigClown-RPI-Azure/raw/master/IoTHubDevice.png)

### use [Device Explorer](https://github.com/Azure/azure-iot-sdk-csharp/tree/master/tools/DeviceExplorer) to monitor that data are correctly received by IoTHub
![device exlorer](https://github.com/PiDiBi/BigClown-RPI-Azure/raw/master/DeviceExplorerMonitor.png)


## Stream Analytics
use following code for parsing data from IoT Hub
```sql
SELECT
    [input].address,
    [input].name,
    [input].id,
    dataReading.PropertyName as dataName,
    CASE WHEN dataReading.PropertyName = 'temperature' THEN GetArrayElement(dataReading.PropertyValue, 0) ELSE dataReading.PropertyValue END as dataValue,     
    [input].data
INTO
    [output]
FROM
    [input]
CROSS APPLY GetRecordProperties([input].data) AS dataReading
```
