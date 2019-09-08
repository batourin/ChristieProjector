using Crestron.RAD.DeviceTypes.Display;

namespace Crestron.RAD.Drivers.Displays
{
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
    }
}
