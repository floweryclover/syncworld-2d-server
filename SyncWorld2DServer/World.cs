using System.Collections.Concurrent;

namespace SyncWorld2DServer
{
    internal class World
    {
        private ConcurrentDictionary<uint, uint> _playerCharacters;
        private uint _newEntityId;
        private readonly object _newEntityIdLock;

        // 컴포넌트
        private ConcurrentDictionary<uint, ValueTuple<float, float>> _playerPosition;
        
        public World()
        {
            _playerCharacters = new ConcurrentDictionary<uint, uint>();
            _newEntityId = 0;        
            _newEntityIdLock = new object();

            _playerPosition = new ConcurrentDictionary<uint, ValueTuple<float, float>>();
        }

        public bool SpawnPlayerCharacter(uint owningPlayerId, out uint spawnedEntityId)
        {
            lock (_newEntityIdLock)
            {
                if (_playerCharacters.TryAdd(owningPlayerId, _newEntityId))
                {
                    spawnedEntityId = _newEntityId;
                    _playerPosition.TryAdd(_newEntityId, new ValueTuple<float, float>(0.0f, 0.0f));
                    _newEntityId++;
                    return true;
                }
                else
                {
                    spawnedEntityId = 0;
                    return false;
                }
            }
        }

        public void DespawnPlayerCharacter(uint owningPlayerId)
        {
            uint entityId;
            if (!_playerCharacters.TryRemove(owningPlayerId, out entityId))
            {
                return;
            }

            _playerPosition.Remove(entityId, out _);
        }

        public bool TryGetPlayerPosition(uint playerId, out ValueTuple<float, float> position) => _playerPosition.TryGetValue(playerId, out position);
    }
}
