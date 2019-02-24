namespace SampleModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;
    using Windows.Devices.Enumeration;
    using Windows.Devices.Gpio;
    using Windows.Devices.I2c;

    using EdgeModuleSamples.Common;
    using EdgeModuleSamples.Devices;
    using static EdgeModuleSamples.Common.AsyncHelper;

    class Program
    {
        static AppOptions Options;
        static ModuleClient ioTHubModuleClient;
        static readonly Random Rnd = new Random();

        static async Task Main(string[] args)
        {
            try
            {
                //
                // Parse options
                //

                Options = new AppOptions();

                Options.Parse(args);

                if (Options.Exit)
                    return;

                //
                // Open Device
                //

                using (var device = await Si7021.Open() )
                {
                    if (null == device)
                        throw new ApplicationException($"Unable to open Si7021. Please ensure that no other applications are using this device.");

                    //
                    // Dump device info
                    //

                    Log.WriteLineRaw($"        Model: {device.Model}");
                    Log.WriteLineRaw($"Serial Number: {device.SerialNumber}");
                    Log.WriteLineRaw($" Firmware Rev: {device.FirmwareRevision}");

                    //
                    // Get some readings
                    //

                    int times = 5;
                    while(times-- > 0)
                    {
                        device.Update();
                        Log.WriteLine($"     Humidity: {device.Humidity:0.0}%");
                        Log.WriteLine($"  Temperature: {device.Temperature:0.0}C");

                        await Task.Delay(1000);
                    }

                    //
                    // Init module client
                    //

                    if (Options.UseEdge)
                    {
                        Init().Wait();
                    }

#if nope
                    // Wait until the app unloads or is cancelled
                    //if (Options.Receive || Options.Transmit)
                    {
                        var cts = new CancellationTokenSource();
                        AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                        Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                        WhenCancelled(cts.Token).Wait();
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                Log.WriteLineException(ex);
            }        
        }

        private static byte[] I2CReadWrite(I2cDevice device, byte[] writeBuffer, int readsize)
        {
            byte[] readBuffer = new byte[readsize];
            var result = device.WriteReadPartial(writeBuffer,readBuffer);
            Log.WriteLineVerbose(result.ToString());

            var display = readBuffer.Aggregate(new StringBuilder(),(sb,b)=>sb.Append(string.Format("{0:X2} ",b)));
            Log.WriteLineVerbose(display.ToString());

            return readBuffer;
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
            AmqpTransportSettings amqpSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            ITransportSettings[] settings = { amqpSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Log.WriteLine($"IoT Hub module client initialized.");
        }
    }
}
