using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using System.Threading;
using System.Diagnostics;
using System.IO;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;
// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace BackgroundApplication1
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral deferral;
        SerialDevice serialPort = null;        
        StreamReader streamReaderObject = null;

        private DeviceInformation[] listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        // iot hub
        static DeviceClient deviceClient;
        static string iotHubUri = "xxx.azure-devices.net";
        static string deviceKey = "xxx";
        static string deviceName = "cloun";

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();

            deviceClient = DeviceClient.Create(iotHubUri,
               new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey),
               TransportType.Amqp);

            ListAvailablePorts();
            var portId = ((DeviceInformation)listOfDevices.First(x => x.Name.StartsWith("STM32"))).Id;            
            Start(portId).GetAwaiter().GetResult();
            
        }
        private async Task Start(string portId)
        {
            await SetupSerial(portId);
            await Listen();
        }
        private async Task SetupSerial(string portId)
        {
            try
            {
                serialPort = await SerialDevice.FromIdAsync(portId);

                if (serialPort == null) return;
                //https://social.msdn.microsoft.com/Forums/en-US/b9633593-377e-4d6f-b3a9-838de0555371/serialdevicefromidasync-always-returns-null-unless-the-serial-adapter-is-plugged-in-after-boot?forum=WindowsIoT
                // add capatibilities to package.appxmanifest and stop z-wave at reaspberry
                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 9600;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                Debug.WriteLine("Serial port configured successfully: ");
                Debug.Write(serialPort.BaudRate + "-");
                Debug.Write(serialPort.DataBits + "-");
                Debug.Write(serialPort.Parity.ToString() + "-");
                Debug.Write(serialPort.StopBits);
                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async Task Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    streamReaderObject = new StreamReader(serialPort.InputStream.AsStreamForRead());
                    
                    // keep reading the serial input
                    while (true)
                    {
                        try
                        {
                            await ReadAsync(ReadCancellationTokenSource.Token);
                        }
                        catch (Exception rex)
                        {
                            Debug.WriteLine(rex);
                        }                        
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Reading task was cancelled, closing device and cleaning up");
                CloseDevice();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {                
                if (streamReaderObject != null)
                {
                    streamReaderObject.Dispose();
                    streamReaderObject = null;
                }

            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();
            var msg = await streamReaderObject.ReadLineAsync();
            Debug.WriteLine(msg);
            dynamic msgObj = JsonConvert.DeserializeObject(msg);

            var data = msgObj[1];
            var adressArr = ((string)msgObj[0]).Split('/');
            var msgToSend = JsonConvert.SerializeObject(new { address = adressArr[0], name = adressArr[1], id = adressArr[2], data = data });
            Debug.WriteLine(msgToSend);
            SendDeviceToCloudMessagesAsync(msgToSend);
        }

        private static async void SendDeviceToCloudMessagesAsync(string msg)
        {
            var message = new Message(Encoding.ASCII.GetBytes(msg));
            await deviceClient.SendEventAsync(message);
            Debug.WriteLine("{0} > Sending message: {1}", DateTime.Now, msg);
            Task.Delay(1000).Wait();

        }
        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);
                listOfDevices = dis.ToArray();                

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
