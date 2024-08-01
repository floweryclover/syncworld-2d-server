using SyncWorld2DProtocol;
using SyncWorld2DProtocol.Cts;
using System.Net.Sockets;

namespace SyncWorld2DServer
{
    internal class Context : IDisposable
    {
        private uint _id;
        public uint Id => _id;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private RingBuffer _receiveRingBuffer;
        private RingBuffer _sendRingBuffer;
        private SemaphoreSlim _sendRingBufferSemaphore;
        private Handler _handler;
        private byte[] _headerSerializationBuffer;
        private readonly string _toStringCache;
        private World _world;
        public World World => _world;

        public Context(TcpClient tcpClient, uint id, World world)
        {
            _id = id;
            _tcpClient = tcpClient;
            _networkStream = _tcpClient.GetStream();
            _receiveRingBuffer = new RingBuffer(Protocol.MaxMessageSize * 1024);
            _sendRingBuffer = new RingBuffer(Protocol.MaxMessageSize * 1024);
            _sendRingBufferSemaphore = new SemaphoreSlim(1);
            _handler = new Handler(this);
            _headerSerializationBuffer = new byte[Protocol.HeaderSize];
            _world = world;
            _toStringCache = $"Context({_id}, {_tcpClient.Client.RemoteEndPoint})";
        }

        public void OnDisconnected()
        {
        }

        public void Dispose()
        {
            _tcpClient.Close();
        }

        public override string ToString()
        {
            return _toStringCache;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task ReceiveAndHandle(CancellationToken cancellationToken)
        {
            try
            {
                var receivedSize = await _networkStream.ReadAsync(_receiveRingBuffer.WritableOnceArraySegment, cancellationToken);
                _receiveRingBuffer.UpdateWritten(receivedSize);

                if (receivedSize == 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
            }
            catch (OperationCanceledException) { }
            (_handler as ICtsHandler).Handle(_receiveRingBuffer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task SendFlush(CancellationToken cancellationToken)
        {
            int readableOnceSize;
            await _sendRingBufferSemaphore.WaitAsync();
            try
            {
                readableOnceSize = _sendRingBuffer.ReadableOnceSize;
            }
            finally
            {
                _sendRingBufferSemaphore.Release();
            }

            if (readableOnceSize == 0)
            {
                await Task.Delay(100);
                return;
            }

            await _sendRingBufferSemaphore.WaitAsync();
            try
            {
                if (_sendRingBuffer.ReadableOnceSize == 0)
                {
                    return;
                }

                await _networkStream.WriteAsync(_sendRingBuffer.ReadableOnceArraySegment, cancellationToken);
                _sendRingBuffer.UpdateRead(_sendRingBuffer.ReadableOnceSize);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _sendRingBufferSemaphore.Release();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="headerMessageId"></param>
        /// <param name="message"></param>
        /// <exception cref="InternalBufferOverflowException"></exception>
        public void WriteMessage<T>(int headerMessageId, ref T message) where T : struct
        {
            _sendRingBufferSemaphore.Wait();
            try
            {
                if (!Serializer.TrySerializeTo(_headerSerializationBuffer, headerMessageId, ref message, _sendRingBuffer))
                {
                    throw new InternalBufferOverflowException($"{this}의 송신 버퍼가 가득 찼습니다.");
                }
            }
            finally
            {
                _sendRingBufferSemaphore.Release();
            }
        }
    }
}
