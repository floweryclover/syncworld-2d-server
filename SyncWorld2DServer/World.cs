using SyncWorld2DProtocol.Stc;
using SyncWorld2DProtocol;
using System.Collections.Concurrent;

namespace SyncWorld2DServer
{
    internal class World
    {
        private ConcurrentDictionary<uint, Context> _players;
        private ConcurrentDictionary<uint, uint> _playerCharacters;
        private ConcurrentDictionary<uint, object?> _entities;
        private uint _newEntityId;
        private readonly object _newEntityIdLock;

        // 컴포넌트
        private ConcurrentDictionary<uint, (float, float)> _entityPosition;
        private ConcurrentDictionary<uint, (float, float, float)> _entityColor;
        private ConcurrentDictionary<uint, (float, float, float, float)> _entityMovement; // vx, vy, ax, ay
        private ConcurrentDictionary<uint, (float, float)> _entityBoxCollision; // width, height
        
        public World()
        {
            _players = new ConcurrentDictionary<uint, Context>();
            _playerCharacters = new ConcurrentDictionary<uint, uint>();
            _entities = new ConcurrentDictionary<uint, object?>();
            _newEntityId = 0;        
            _newEntityIdLock = new object();

            _entityPosition = new ConcurrentDictionary<uint, (float, float)>();
            _entityColor = new ConcurrentDictionary<uint, (float, float, float)>();
            _entityMovement = new ConcurrentDictionary<uint, (float, float, float, float)>();
            _entityBoxCollision = new ConcurrentDictionary<uint, (float, float)>();
        }

        public bool SpawnPlayerCharacter(uint owningPlayerId, out uint spawnedEntityId)
        {
            lock (_newEntityIdLock)
            {
                if (!_playerCharacters.TryAdd(owningPlayerId, _newEntityId))
                {
                    spawnedEntityId = 0;
                    return false;
                }
                spawnedEntityId = _newEntityId;
                _newEntityId++;
            }

            _entities.TryAdd(spawnedEntityId, null);
            var spawnPosition = new ValueTuple<float, float>(0.0f, 5.0f);
            var random = new Random();
            var color = new ValueTuple<float, float, float>(random.NextSingle(), random.NextSingle(), random.NextSingle());

            _entityPosition.TryAdd(spawnedEntityId, spawnPosition);
            _entityColor.TryAdd(spawnedEntityId, color);

            var spawnEntityMessage = new SpawnEntityMessage() { EntityId = spawnedEntityId, X = spawnPosition.Item1, Y = spawnPosition.Item2 };
            var assignEntityColorMessage = new AssignEntityColorMessage { EntityId = spawnedEntityId, R = color.Item1, G = color.Item2, B = color.Item3 };

            Broadcast(Protocol.StcSpawnEntity, ref spawnEntityMessage);
            Broadcast(Protocol.StcAssignEntityColor, ref assignEntityColorMessage);

            return true;
        }

        public void SpawnSoccerBall()
        {
            uint spawnedEntityId;
            lock (_newEntityIdLock)
            {
                spawnedEntityId = _newEntityId;
                _newEntityId++;
            }

            _entities.TryAdd(spawnedEntityId, null);
            _entityPosition.TryAdd(spawnedEntityId, (0.0f, 10.0f));
            _entityBoxCollision.TryAdd(spawnedEntityId, (10.0f, 10.0f));
            _entityMovement.TryAdd(spawnedEntityId, (0.0f, 0.0f, 0.0f, 0.0f));
        }

        public void DespawnEntity(uint entityId)
        {
            if (!_entities.ContainsKey(entityId))
            {
                return;
            }

            var despawnEntityMessage = new DespawnEntityMessage() { EntityId = entityId };
            Broadcast(Protocol.StcDespawnEntity, ref despawnEntityMessage);

            _entityPosition.Remove(entityId, out _);
            _entityColor.Remove(entityId, out _);
            _entityMovement.Remove(entityId, out _);
            _entityBoxCollision.Remove(entityId, out _);
            _entities.Remove(entityId, out _);
        }

        public void DespawnPlayerCharacter(uint owningPlayerId)
        {
            uint entityId;
            if (!_playerCharacters.TryRemove(owningPlayerId, out entityId))
            {
                return;
            }
            DespawnEntity(entityId);
        }

        public bool TryGetEntityPosition(uint entityId, out ValueTuple<float, float> position) => _entityPosition.TryGetValue(entityId, out position);

        public void OnPlayerJoined(uint playerId, Context context)
        {
            if (_players.ContainsKey(playerId))
            {
                throw new InvalidOperationException($"이미 월드에 플레이어 {playerId}가 존재하는 상태에서 추가하려고 시도했습니다.");
            }
            _players.TryAdd(playerId, context);

            foreach (var entityId in _playerCharacters.Values)
            {
                if (_entityPosition.TryGetValue(entityId, out var position))
                {
                    var spawnEntityMessage = new SpawnEntityMessage() { EntityId = entityId, X = position.Item1, Y = position.Item2 };
                    _players[playerId].WriteMessage(Protocol.StcSpawnEntity, ref spawnEntityMessage);
                }

                if (_entityColor.TryGetValue(entityId, out var color))
                {
                    var assignEntityColorMessage = new AssignEntityColorMessage() { EntityId = entityId, R = color.Item1, G = color.Item2, B = color.Item3 };
                    _players[playerId].WriteMessage(Protocol.StcAssignEntityColor, ref assignEntityColorMessage);
                }
            }
        }

        public void OnPlayerLeft(uint playerId)
        {
            if (!_players.ContainsKey(playerId))
            {
                return;
            }
            DespawnPlayerCharacter(playerId);
            _players.TryRemove(playerId, out _);
        }

        public void MovePlayerCharacter(uint playerId, float positionX, float positionY, float velocityX, float velocityY, float accelerationX, float accelerationY)
        {
            if (!_playerCharacters.TryGetValue(playerId, out var entityId))
            {
                return;
            }

            if (!_entityPosition.ContainsKey(entityId))
            {
                return;
            }

            var pingDeltaX = velocityX * 0.01f; // 10ms의 핑동안 이동한 X 거리
            var pingDeltaY = velocityY * 0.01f; // 10ms의 핑동안 이동한 Y 거리

            var beginX = positionX + pingDeltaX; // 클라이언트가 보낸 X좌표 + 10ms의 핑동안 이동한 거리 
            var beginY = positionY + pingDeltaX; // 클라이언트가 보낸 Y좌표 + 10ms의 핑동안 이동한 거리

            var targetX = beginX + velocityX * 0.1f + 0.5f * accelerationX * 0.01f; // 다음 주기에 받을 클라이언트 X좌표의 예상된 값
            var targetY = beginY + velocityY * 0.1f + 0.5f * accelerationY * 0.01f; // 다음 주기에 받을 클라이언트 Y좌표의 예상된 값
            _entityPosition[entityId] = (beginX, beginY);

            var message = new MoveEntityMessage() { EntityId = entityId, X = targetX, Y = targetY };
            BroadcastExcluding(Protocol.StcMoveEntity, ref message, playerId);
        }

        public void Update(double deltaTime)
        {
            foreach (var kvp in _entityPosition)
            {
                var entityId = kvp.Key;
                var position = kvp.Value;

                if (position.Item2 < -100.0f)
                {
                    var teleportX = 0.0f;
                    var teleportY = 10.0f;
                    _entityPosition[entityId] = (teleportX, teleportY);

                    var message = new TeleportEntityMessage() { EntityId = entityId, X = teleportX, Y = teleportY };
                    Broadcast(Protocol.StcTeleportEntity, ref message);
                }
            }
        }

        private void BroadcastExcluding<T>(int messageId, ref T message, uint excludingPlayerId) where T : struct => BroadcastImpl<T>(messageId, ref message, excludingPlayerId);

        private void Broadcast<T>(int messageId, ref T message) where T : struct => BroadcastImpl<T>(messageId, ref message, null);

        private void BroadcastImpl<T>(int messageId, ref T message, uint? excludingPlayerId) where T : struct
        {
            foreach (var context in _players.Values)
            {
                if (excludingPlayerId != null && excludingPlayerId == context.Id)
                {
                    continue;
                }
                context.WriteMessage(messageId, ref message);
            }
        }
    }
}
