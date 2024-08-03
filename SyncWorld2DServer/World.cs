using SyncWorld2DProtocol.Stc;
using SyncWorld2DProtocol;
using System.Collections.Concurrent;
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;

namespace SyncWorld2DServer
{
    internal partial class World
    {
        private ConcurrentDictionary<uint, Context> _players;
        private ConcurrentDictionary<uint, uint> _playerCharacters;
        private ConcurrentDictionary<uint, uint> _entities; // 0: 플랫폼, 1: 공, 2: 플레이어
        private uint _newEntityId;
        private readonly object _newEntityIdLock;

        // 컴포넌트
        private ConcurrentDictionary<uint, (float, float)> _entityPosition;
        private ConcurrentDictionary<uint, (float, float, float)> _entityColor;
        private ConcurrentDictionary<uint, (float, float, bool)> _entityMovement; // vx, vy, isGrounded
        private ConcurrentDictionary<uint, (float, float)> _entityBoxCollision; // width, height
        private ConcurrentDictionary<uint, object?> _entityServerAuthority;
        private ConcurrentDictionary<uint, object?> _entityGravity;
        
        public World()
        {
            _players = new ConcurrentDictionary<uint, Context>();
            _playerCharacters = new ConcurrentDictionary<uint, uint>();
            _entities = new ConcurrentDictionary<uint, uint>();
            _newEntityId = 0;        
            _newEntityIdLock = new object();

            _entityPosition = new ConcurrentDictionary<uint, (float, float)>();
            _entityColor = new ConcurrentDictionary<uint, (float, float, float)>();
            _entityMovement = new ConcurrentDictionary<uint, (float, float, bool)>();
            _entityBoxCollision = new ConcurrentDictionary<uint, (float, float)>();
            _entityServerAuthority = new ConcurrentDictionary<uint, object?>();
            _entityGravity = new ConcurrentDictionary<uint, object?>();

            SpawnSoccerBall();
            SpawnPlatform();
            Task.Run(SyncEntities);
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

            _entities.TryAdd(spawnedEntityId, 2);
            var spawnPosition = new ValueTuple<float, float>(0.0f, 5.0f);
            var random = new Random();
            var color = new ValueTuple<float, float, float>(random.NextSingle(), random.NextSingle(), random.NextSingle());

            _entityPosition.TryAdd(spawnedEntityId, spawnPosition);
            _entityColor.TryAdd(spawnedEntityId, color);
            _entityBoxCollision.TryAdd(spawnedEntityId, (1.0f, 2.0f));
            _entityMovement.TryAdd(spawnedEntityId, (0.0f, 0.0f, false));

            var spawnEntityMessage = new SpawnEntityMessage() { EntityId = spawnedEntityId, EntityType = 2, X = spawnPosition.Item1, Y = spawnPosition.Item2 };
            var assignEntityColorMessage = new AssignEntityColorMessage { EntityId = spawnedEntityId, R = color.Item1, G = color.Item2, B = color.Item3 };

            Broadcast(Protocol.StcSpawnEntity, ref spawnEntityMessage);
            Broadcast(Protocol.StcAssignEntityColor, ref assignEntityColorMessage);

            return true;
        }

        private void SpawnPlatform()
        {
            uint spawnedEntityId;
            lock (_newEntityIdLock)
            {
                spawnedEntityId = _newEntityId;
                _newEntityId++;
            }

            _entities.TryAdd(spawnedEntityId, 0);
            _entityPosition.TryAdd(spawnedEntityId, (0.0f, 0.0f));
            _entityBoxCollision.TryAdd(spawnedEntityId, (50.0f, 1.0f));

            Console.WriteLine($"플랫폼 Entity ID: {spawnedEntityId}");
        }

        private void SpawnSoccerBall()
        {
            uint spawnedEntityId;
            lock (_newEntityIdLock)
            {
                spawnedEntityId = _newEntityId;
                _newEntityId++;
            }

            var message = new SpawnEntityMessage() { EntityId = spawnedEntityId, EntityType = 1, X = 0.0f, Y = 10.0f };
            Broadcast(Protocol.StcSpawnEntity, ref message);

            _entities.TryAdd(spawnedEntityId, 1);
            _entityPosition.TryAdd(spawnedEntityId, (0.0f, 10.0f));
            _entityBoxCollision.TryAdd(spawnedEntityId, (1.0f, 1.0f));
            _entityMovement.TryAdd(spawnedEntityId, (0.0f, 0.0f, false));
            _entityServerAuthority.TryAdd(spawnedEntityId, null);
            _entityGravity.TryAdd(spawnedEntityId, null);

            Console.WriteLine($"공 Entity ID: {spawnedEntityId}");
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
            _entityServerAuthority.Remove(entityId, out _);
            _entityGravity.Remove(entityId, out _);
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

            foreach (var entityKvp in _entities)
            {
                var entityId = entityKvp.Key;
                var entityType = entityKvp.Value;
                if (entityType == 0)
                {
                    continue;
                }
                if (_entityPosition.TryGetValue(entityId, out var position))
                {
                    var spawnEntityMessage = new SpawnEntityMessage() { EntityId = entityId, EntityType = entityType, X = position.Item1, Y = position.Item2 };
                    _players[playerId].WriteMessage(Protocol.StcSpawnEntity, ref spawnEntityMessage);
                }

                if (entityType == 1)
                {
                    continue;
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
            _entityMovement[entityId] = (velocityX, velocityY, false);

            var message = new MoveEntityMessage() { EntityId = entityId, X = targetX, Y = targetY };
            BroadcastExcluding(Protocol.StcMoveEntity, ref message, playerId);
        }

        public void Update(float deltaTime)
        {
            // 허용 영역 벗어날경우 원점 텔레포트
            foreach (var kvp in _entityPosition)
            {
                var entityId = kvp.Key;
                var position = kvp.Value;

                if (position.Item2 < -50.0f || Math.Abs(position.Item1) > 100.0f)
                {
                    var teleportX = 0.0f;
                    var teleportY = 10.0f;
                    _entityPosition[entityId] = (teleportX, teleportY);

                    if (_entityServerAuthority.ContainsKey(entityId) && _entityMovement.ContainsKey(entityId))
                    {
                        _entityMovement[entityId] = (0.0f, 0.0f, false);
                    }

                    var message = new TeleportEntityMessage() { EntityId = entityId, X = teleportX, Y = teleportY };
                    Broadcast(Protocol.StcTeleportEntity, ref message);
                }
            }

            // 충돌 판정
            foreach (var myKvp in _entityBoxCollision)
            {
                var myEntityId = myKvp.Key;
                var myCollisionWidth = myKvp.Value.Item1;
                var myCollisionHeight = myKvp.Value.Item2;

                if (!_entityPosition.ContainsKey(myEntityId))
                {
                    continue;
                }

                var myX = _entityPosition[myEntityId].Item1;
                var myY = _entityPosition[myEntityId].Item2;
                var myLeft = _entityPosition[myEntityId].Item1 - (myCollisionWidth) / 2;
                var myRight = _entityPosition[myEntityId].Item1 + (myCollisionWidth) / 2;
                var myTop = _entityPosition[myEntityId].Item2 + (myCollisionHeight) / 2;
                var myBottom = _entityPosition[myEntityId].Item2 - (myCollisionHeight) / 2;

                foreach (var opponentKvp in _entityBoxCollision)
                {
                    var opponentEntityId = opponentKvp.Key;
                    var opponentCollisionWidth = opponentKvp.Value.Item1;
                    var opponentCollisionHeight = opponentKvp.Value.Item2;

                    // 만약 나 자신과의 충돌이거나, 위치가 없거나, 둘 다 서버 소유가 아니라면 무시
                    if (myEntityId == opponentEntityId
                        || !_entityPosition.ContainsKey(opponentEntityId)
                        || !_entityServerAuthority.ContainsKey(myEntityId) && !_entityServerAuthority.ContainsKey(opponentEntityId))
                    {
                        continue;
                    }

                    var opponentX = _entityPosition[opponentEntityId].Item1;
                    var opponentY = _entityPosition[opponentEntityId].Item2;
                    var opponentLeft = _entityPosition[opponentEntityId].Item1 - (opponentCollisionWidth) / 2;
                    var opponentRight = _entityPosition[opponentEntityId].Item1 + (opponentCollisionWidth) / 2;
                    var opponentTop = _entityPosition[opponentEntityId].Item2 + (opponentCollisionHeight) / 2;
                    var opponentBottom = _entityPosition[opponentEntityId].Item2 - (opponentCollisionHeight) / 2;

                    float opponentVelocityX = 0.0f;
                    float opponentVelocityY = 0.0f;
                    if (_entityMovement.TryGetValue(opponentEntityId, out var value))
                    {
                        opponentVelocityX = value.Item1;
                        opponentVelocityY = value.Item2;
                    }

                    var isCollidingX = myRight >= opponentLeft && myLeft <= opponentRight;
                    var isCollidingY = myBottom <= opponentTop && myTop >= opponentBottom;

                    if (!isCollidingX || !isCollidingY)
                    {
                        continue;
                    }

                    if (_entityMovement.ContainsKey(myEntityId))
                    {
                        var newVelocityX = _entityMovement[myEntityId].Item1;
                        var newVelocityY = _entityMovement[myEntityId].Item2;

                        if ((myX < opponentLeft && newVelocityX >= 0.0f)
                            || (myX > opponentRight && newVelocityX <= 0.0f))
                        {
                            newVelocityX = -newVelocityX;// * 0.8f;
                            if (opponentVelocityX != 0.0f)
                            {
                                newVelocityX = opponentVelocityX;
                            }

                            if (Math.Abs(newVelocityX) < 0.1f)
                            {
                                newVelocityX = 0.0f;
                            }
                        }

                        if ((myY < opponentBottom && newVelocityY >= 0.0f)
                            || (myY > opponentTop && newVelocityY <= 0.0f))
                        {
                            newVelocityY = -newVelocityY;// 0.8f;
                            if (Math.Abs(newVelocityY) < 0.1f)
                            {
                                newVelocityY = 0.0f;
                            }
                            if (opponentVelocityY != 0.0f)
                            {
                                newVelocityY = opponentVelocityY;
                            }
                        }

                        bool isGrounded = isCollidingY && newVelocityY == 0.0f && myY >= opponentY;
                        _entityMovement[myEntityId] = (newVelocityX, newVelocityY, isGrounded);
                    }
                }
            }

            // 서버 소유 오브젝트의 이동 갱신
            foreach (var serverAuthoritativeEntityId in _entityServerAuthority.Keys)
            {
                if (!_entityMovement.ContainsKey(serverAuthoritativeEntityId)
                    || !_entityPosition.ContainsKey(serverAuthoritativeEntityId))
                {
                    continue;
                }

                var currentVelocityX = _entityMovement[serverAuthoritativeEntityId].Item1;
                var currentVelocityY = _entityMovement[serverAuthoritativeEntityId].Item2;
                var isGrounded = _entityMovement[serverAuthoritativeEntityId].Item3;

                var newVelocityX = currentVelocityX - (currentVelocityX * 0.1f) * deltaTime;
                var newVelocityY = currentVelocityY + -9.81f * deltaTime;

                if (isGrounded)
                {
                    newVelocityY = 0.0f;
                }

                _entityMovement[serverAuthoritativeEntityId] = (newVelocityX, newVelocityY, isGrounded);

                var currentX = _entityPosition[serverAuthoritativeEntityId].Item1;
                var currentY = _entityPosition[serverAuthoritativeEntityId].Item2;

                var newX = currentX + newVelocityX * deltaTime;
                var newY = currentY + newVelocityY * deltaTime;

                _entityPosition[serverAuthoritativeEntityId] = (newX, newY);
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
