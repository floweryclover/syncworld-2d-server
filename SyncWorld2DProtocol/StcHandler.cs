using MemoryPack;
using System;

namespace SyncWorld2DProtocol.Stc
{
    public interface IStcHandler
    {
        bool OnSpawnEntity(uint entityId, float x, float y);
        bool OnDespawnEntity(uint entityId);
        bool OnPossessEntity(uint entityId);
        bool OnUnpossessEntity();

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
                    case Protocol.StcSpawnEntity:
                        {
                            var spawnEntityMessage = MemoryPackSerializer.Deserialize<SpawnEntityMessage>(body);
                            shouldContinue = OnSpawnEntity(spawnEntityMessage.EntityId, spawnEntityMessage.X, spawnEntityMessage.Y);
                            break;
                        }
                    case Protocol.StcDespawnEntity:
                        {
                            var despawnEntityMessage = MemoryPackSerializer.Deserialize<DespawnEntityMessage>(body);
                            shouldContinue = OnDespawnEntity(despawnEntityMessage.EntityId);
                            break;
                        }
                    case Protocol.StcPossessEntity:
                        {
                            var possessEntityMessage = MemoryPackSerializer.Deserialize<PossessEntityMessage>(body);
                            shouldContinue = OnPossessEntity(possessEntityMessage.EntityId);
                            break;
                        }
                    case Protocol.StcUnpossessEntity:
                        {
                            var unpossessEntityMessage = MemoryPackSerializer.Deserialize<UnpossessEntityMessage>(body);
                            shouldContinue = OnUnpossessEntity();
                            break;
                        }
                    default:
                        {
                            throw new InvalidProgramException($"알 수 없는 STC 메시지 ID를 수신했습니다: {headerMessageId}");
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
    public partial struct SpawnEntityMessage
    {
        public uint EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial struct DespawnEntityMessage
    {
        public uint EntityId { get; set; }
    }

    [MemoryPackable]
    public partial struct PossessEntityMessage
    {
        public uint EntityId { get; set; }
    }

    [MemoryPackable]
    public partial struct UnpossessEntityMessage { }
}
