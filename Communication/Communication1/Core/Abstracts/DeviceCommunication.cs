using Communication.Buffers;
using Communication.Core.Definition;
using Communication.Core.Interfaces;
using Communication.Tools.ValueDecoder;
using Prism.Ioc;
using System.Threading;
using System.Threading.Tasks;
using static Communication.Core.Definition.CommunicationParam;
 

namespace Communication.Core.Abstracts
{
    public abstract class DeviceCommunication : ICommunication, ICanAutoRead
    {
        public DeviceCommunication(IContainerProvider container)
        {
            mCommunicationBuffer = container.Resolve<ICommunicationBuffer>();
        }
        /// <summary>
        /// Í¨Ñ¶»º´æÇø
        /// </summary>
        protected ICommunicationBuffer mCommunicationBuffer;

        public abstract ICommunicationDataPoint this[string name] { get; }

        public string Name { get; set; }

        public string ID { get; set; }

        public abstract bool IsConnected { get; set; }

        public int TimeOutMs { get; set; }

        public virtual IInputConverter InputConverter { get; set; }

        public IValueDecoder ValueDecoder { get; set; } = new Tools.ValueDecoder.WordValueDecoder();

        public abstract DeviceProtocolType CommunicationType { get; }

        public abstract bool CanAutoRead { get; }

        public abstract Task Connect();

        public abstract void Disconnect();

        public abstract Task ReadAsync(CancellationToken ct = default);

        public abstract Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default);

        public abstract void Write(params (ICommunicationDataPoint dp, object value)[] pvs);

        public abstract void Write(ICommunicationDataPoint dp, object value);

        public abstract Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs);

        public abstract Task WriteAsync(ICommunicationDataPoint dp, object value);
    }
}
