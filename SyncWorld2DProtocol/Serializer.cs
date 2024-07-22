using MemoryPack;
using System;
using System.IO;

namespace SyncWorld2DProtocol
{
    public class Serializer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <param name="headerMessageId"></param>
        /// <param name="message"></param>
        /// <param name="ringBuffer"></param>
        public static bool TrySerializeTo<T>(byte[] headerSerializebuffer, int headerMessageId, ref T message, RingBuffer ringBuffer) where T : struct
        {
            if (headerSerializebuffer.Length < Protocol.HeaderSize)
            {
                throw new InternalBufferOverflowException($"헤더 직렬화를 위한 buffer의 크기는 {Protocol.HeaderSize} 이상이어야 합니다.");
            }

            var serialized = MemoryPackSerializer.Serialize<T>(message);
            var headerBodySize = (ushort)serialized.Length;

            if (ringBuffer.WritableTotalSize < Protocol.HeaderSize + headerBodySize)
            {
                return false;
            }
            BitConverter.TryWriteBytes(new Span<byte>(headerSerializebuffer, 0, 2), headerBodySize);
            BitConverter.TryWriteBytes(new Span<byte>(headerSerializebuffer, 2, 2), (ushort)headerMessageId);
            ringBuffer.Write(headerSerializebuffer, 4);
            ringBuffer.Write(serialized, headerBodySize);

            return true;
        }
    }
}
