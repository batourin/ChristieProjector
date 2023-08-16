using Crestron.RAD.DeviceTypes.Display;

namespace Crestron.RAD.Drivers.Displays
{
    using System;
    using Crestron.RAD.Common.BasicDriver;
    using Crestron.RAD.Common.Interfaces;
    using Crestron.RAD.Common.Transports;

    using Crestron.SimplSharp;

    public class ChristieProjectorTcp : ABasicVideoDisplay, ITcp
    {

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public ChristieProjectorTcp()
        {
            try
            {
                // References to Tcp2Capability must be contained within
                // another method so that we are able to catch the 
                // TypeLoadException that occurs when running on a system
                // with an older version of RADCommon. If the code within 
                // AddCapabilities is inlined here, then the 
                // TypeLoadException will occur before this can catch it.

                AddCapabilities();
            }
            catch (TypeLoadException)
            {
                // This exception would happen if this driver was loaded
                // on a system running RADCommon without ITcp2 or 
                // IDeviceCapability.
            }
        }

        private void AddCapabilities()
        {
            // Adds the Tcp2 capability to allow applications to use a 
            // hostname when initializing the driver.
            // The Tcp2Capability class allows us to implement the ITcp2 
            // interface without defining a class implementing ITcp2 in 
            // this assembly (which would cause a TypeLoadException when 
            // the application searches through the defined types to locate 
            // the driver).

            var tcp2Capability = new Tcp2Capability(Initialize);
            Capabilities.RegisterInterface(typeof(ITcp2), tcp2Capability);
        }

        public void Initialize(IPAddress ipAddress, int port)
        {
            var tcpTransport = new TcpTransport
            {
                EnableAutoReconnect = EnableAutoReconnect,
                EnableLogging = InternalEnableLogging,
                CustomLogger = InternalCustomLogger,
                EnableRxDebug = InternalEnableRxDebug,
                EnableTxDebug = InternalEnableTxDebug,
            };

            tcpTransport.Initialize(ipAddress, port);
            ConnectionTransport = tcpTransport;

            DisplayProtocol = new ChristieProjectorProtocol(ConnectionTransport, Id)
            {
                EnableLogging = InternalEnableLogging,
                EnableStackTrace = InternalEnableStackTrace,
                CustomLogger = InternalCustomLogger,
            };

            DisplayProtocol.StateChange += StateChange;
            DisplayProtocol.RxOut += SendRxOut;
            DisplayProtocol.Initialize(DisplayData);
        }
        private void Initialize(string address, int port)
        {
            var tcpTransport = new TcpTransport
            {
                EnableAutoReconnect = EnableAutoReconnect,
                EnableLogging = InternalEnableLogging,
                CustomLogger = InternalCustomLogger,
                EnableRxDebug = InternalEnableRxDebug,
                EnableTxDebug = InternalEnableTxDebug
            };

            // Note: This overload accepting a string did not exist in
            // older versions of RADCommon.
            // It may only be called in the code implementing ITcp2 to 
            // ensure it is not called when older versions of RADCommon
            // are in use on the system.

            tcpTransport.Initialize(address, port);
            ConnectionTransport = tcpTransport;

            DisplayProtocol = new ChristieProjectorProtocol(ConnectionTransport, Id)
            {
                EnableLogging = InternalEnableLogging,
                EnableStackTrace = InternalEnableStackTrace,
                CustomLogger = InternalCustomLogger
            };

            DisplayProtocol.StateChange += StateChange;
            DisplayProtocol.RxOut += SendRxOut;
            DisplayProtocol.Initialize(DisplayData);
        }
    }
}
