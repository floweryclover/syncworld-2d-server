using SyncWorld2DProtocol.Stc;
using SyncWorld2DProtocol;
using System.Collections.Concurrent;

namespace SyncWorld2DServer
{
    internal class World
    {
        private ConcurrentDictionary<uint, Context> _players;
        private ConcurrentDictionary<uint, uint> _playerCharacters;
        private uint _newEntityId;
        private readonly object _newEntityIdLock;

        // 컴포넌트
        private ConcurrentDictionary<uint, (float, float)> _entityPosition;
        private ConcurrentDictionary<uint, (float, float, float)> _entityColor;
        
        public World()
        {
            _players = new ConcurrentDictionary<uint, Context>();
            _playerCharacters = new ConcurrentDictionary<uint, uint>();
            _newEntityId = 0;        
            _newEntityIdLock = new object();

            _entityPosition = new ConcurrentDictionary<uint, (float, float)>();
            _entityColor = new ConcurrentDictionary<uint, (float, float, float)>();
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

            var spawnPosition = new ValueTuple<float, float>(0.0f, 5.0f);
            var random = new Random();
            var color = new ValueTuple<float, float, float>(random.NextSingle(), random.NextSingle(), random.NextSingle());

            _entityPosition.TryAdd(spawnedEntityId, spawnPosition);
            _entityColor.TryAdd(spawnedEntityId, color);

            var spawnEntityMessage = new SpawnEntityMessage() { EntityId = spawnedEntityId, X = spawnPosition.Item1, Y = spawnPosition.Item2 };
            var assignEntityColorMessage = new AssignEntityColorMessage { EntityId = spawnedEntityId, R = color.Item1, G = color.Item2, B = color.Item3 };
            foreach (var context in _players.Values)          
            {

                context.WriteMessage(Protocol.StcSpawnEntity, ref spawnEntityMessage);
                context.WriteMessage(Protocol.StcAssignEntityColor, ref assignEntityColorMessage);
            }    

            return true;
        }

        public void DespawnPlayerCharacter(uint owningPlayerId)
        {
            uint entityId;
            if (!_playerCharacters.TryRemove(owningPlayerId, out entityId))
            {
                return;
            }

            foreach (var context in _players.Values)
            {
                var despawnEntityMessage = new DespawnEntityMessage() { EntityId = entityId };
                context.WriteMessage(Protocol.StcDespawnEntity, ref despawnEntityMessage);
            }

            _entityPosition.Remove(entityId, out _);
            _entityColor.Remove(entityId, out _);
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

        public void MovePlayerCharacter(uint playerId, float x, float y)
        {
            if (!_playerCharacters.TryGetValue(playerId, out var entityId))
            {
                return;
            }

            if (!_entityPosition.ContainsKey(entityId))
            {
                return;
            }

            _entityPosition[entityId] = (x, y);

            var message = new MoveEntityMessage() { EntityId = entityId, X = x, Y = y };
            foreach (var context in _players.Values)
            {
                if (context.Id == playerId)
                {
                    continue;
                }
                context.WriteMessage(Protocol.StcMoveEntity, ref message);
            }
        }
    }
}
