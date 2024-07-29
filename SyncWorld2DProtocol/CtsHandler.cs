using MemoryPack;
using System;
using System.IO;

namespace SyncWorld2DProtocol.Cts
{
    public interface ICtsHandler
    {
        bool OnRequestJoin();
        bool OnSendCurrentPosition(float positionX, float positionY, float velocityX, float velocityY, float accelerationX, float accelerationY);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="receiveRingBuffer"></param>
        /// <exception cref="InvalidProgramException"></exception>
        /// <exception cref="InternalBufferOverflowException"></exception>
        /// <exception cref="InvalidProgramException"></exception>
        public sealed void Handle(RingBuffer receiveRingBuffer)
        {
            while (true)
            {
                if (receiveRingBuffer.ReadableTotalSize < Protocol.HeaderSize)
                {
                    return;
                }

                var headerBodySize = BitConverter.ToInt16(receiveRingBuffer.Peek(2));
                if (receiveRingBuffer.ReadableTotalSize < Protocol.HeaderSize + headerBodySize)
                {
                    return;
                }
                receiveRingBuffer.Pop(2);
                var headerMessageId = BitConverter.ToInt16(receiveRingBuffer.Pop(2));
                var body = receiveRingBuffer.Pop(headerBodySize);

                var shouldContinue = false;
                
                switch (headerMessageId)
                {
                    case Protocol.CtsRequestJoin:
                        {
                            var helloClientMessage = MemoryPackSerializer.Deserialize<RequestJoinMessage>(body);
                            shouldContinue = OnRequestJoin();
                            break;
                        }
                    case Protocol.CtsSendCurrentPosition:
                        {
                            var sendCurrentPositionMessage = MemoryPackSerializer.Deserialize<SendCurrentPositionMessage>(body);
                            shouldContinue = OnSendCurrentPosition(
                                sendCurrentPositionMessage.PositionX, 
                                sendCurrentPositionMessage.PositionY,
                                sendCurrentPositionMessage.VelocityX, 
                                sendCurrentPositionMessage.VelocityY,
                                sendCurrentPositionMessage.AccelerationX,
                                sendCurrentPositionMessage.AccelerationY);
                            break;
                        }
                    default:
                        {
                            throw new InvalidProgramException($"알 수 없는 CTS 메시지 ID를 수신했습니다: {headerMessageId}");
                        }
                }

                if (!shouldContinue)
                {
                    break;
                }
            }
        }
    }

    [MemoryPackable]
    public partial struct RequestJoinMessage {}

    [MemoryPackable]
    public partial struct SendCurrentPositionMessage
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        public float VelocityX { get; set; }
        public float VelocityY { get; set; }

        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }

    }
}
