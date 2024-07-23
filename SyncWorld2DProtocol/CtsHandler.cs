using MemoryPack;
using System;
using System.IO;

namespace SyncWorld2DProtocol.Cts
{
    public interface ICtsHandler
    {
        bool OnRequestJoin();

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
}
