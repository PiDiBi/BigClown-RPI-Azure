# BigClown Core - RPI - Azure Connection
- You need to have BigClown Core unit and connect it to Raspberry PI via USB
- upload [firmware](https://github.com/bigclownlabs/bcp-wireless-circus/releases/tag/v1.0.0)  to core unit
- Raspberry PI 2/3 with Win 10 IoT Core and this project
- you need Microsoft Azure subscription
- configure Azure IoT Hub and create device there, get device key/name and fill it in code
- consume from IoT Hub with stream analytics

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
    [powerbilocal]
FROM
    [input]
CROSS APPLY GetRecordProperties([input].data) AS dataReading
```
