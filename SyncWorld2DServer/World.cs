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
        private ConcurrentDictionary<uint, ValueTuple<float, float>> _playerPosition;
        
        public World()
        {
            _players = new ConcurrentDictionary<uint, Context>();
            _playerCharacters = new ConcurrentDictionary<uint, uint>();
            _newEntityId = 0;        
            _newEntityIdLock = new object();

            _playerPosition = new ConcurrentDictionary<uint, ValueTuple<float, float>>();
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

            _playerPosition.TryAdd(spawnedEntityId, new ValueTuple<float, float>(0.0f, 5.0f));

            foreach (var context in _players.Values)
            {
                var spawnEntityMessage = new SpawnEntityMessage() { EntityId = spawnedEntityId, X = 0.0f, Y = 5.0f };
                context.WriteMessage(Protocol.StcSpawnEntity, ref spawnEntityMessage);
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

            _playerPosition.Remove(entityId, out _);
        }

        public bool TryGetEntityPosition(uint entityId, out ValueTuple<float, float> position) => _playerPosition.TryGetValue(entityId, out position);

        public void OnPlayerJoined(uint playerId, Context context)
        {
            if (_players.ContainsKey(playerId))
            {
                throw new InvalidOperationException($"이미 월드에 플레이어 {playerId}가 존재하는 상태에서 추가하려고 시도했습니다.");
            }
            _players.TryAdd(playerId, context);

            foreach (var entityId in _playerCharacters.Values)
            {
                if (_playerPosition.TryGetValue(entityId, out var position))
                {
                    var spawnEntityMessage = new SpawnEntityMessage() { EntityId = entityId, X = position.Item1, Y = position.Item2 };
                    _players[playerId].WriteMessage(Protocol.StcSpawnEntity, ref spawnEntityMessage);
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
    }
}
