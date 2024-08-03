using MemoryPack;
using System;

namespace SyncWorld2DProtocol.Stc
{
    public interface IStcHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="entityType">0은 플레이어, 1은 공</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        bool OnSpawnEntity(uint entityId, uint entityType, float x, float y);
        bool OnDespawnEntity(uint entityId);
        bool OnPossessEntity(uint entityId);
        bool OnUnpossessEntity();
        bool OnMoveEntity(uint entityId, float x, float y);
        bool OnAssignEntityColor(uint entityId, float r, float g, float b);
        bool OnTeleportEntity(uint entityId, float x, float y);

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
                            shouldContinue = OnSpawnEntity(spawnEntityMessage.EntityId, spawnEntityMessage.EntityType, spawnEntityMessage.X, spawnEntityMessage.Y);
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
                    case Protocol.StcMoveEntity:
                        {
                            var moveEntityMessage = MemoryPackSerializer.Deserialize<MoveEntityMessage>(body);
                            shouldContinue = OnMoveEntity(moveEntityMessage.EntityId, moveEntityMessage.X, moveEntityMessage.Y);
                            break;
                        }
                    case Protocol.StcAssignEntityColor:
                        {
                            var assignEntityColorMessage = MemoryPackSerializer.Deserialize<AssignEntityColorMessage>(body);
                            shouldContinue = OnAssignEntityColor(assignEntityColorMessage.EntityId, assignEntityColorMessage.R, assignEntityColorMessage.G, assignEntityColorMessage.B);
                            break;
                        }
                    case Protocol.StcTeleportEntity:
                        {
                            var teleportEntityMessage = MemoryPackSerializer.Deserialize<TeleportEntityMessage>(body);
                            shouldContinue = OnTeleportEntity(teleportEntityMessage.EntityId, teleportEntityMessage.X, teleportEntityMessage.Y);
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
        public uint EntityType { get; set; } // 0은 플레이어, 1은 공
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

    [MemoryPackable]
    public partial struct MoveEntityMessage
    {
        public uint EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [MemoryPackable]
    public partial struct AssignEntityColorMessage
    {
        public uint EntityId { get; set; }
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
    }

    [MemoryPackable]
    public partial struct TeleportEntityMessage
    {
        public uint EntityId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
