using SyncWorld2DProtocol.Stc;
using SyncWorld2DProtocol;

namespace SyncWorld2DServer
{
    internal partial class World
    {
        // 서버 소유 엔티티의 이동 동기화
        public void SyncEntities()
        {
            while (true)
            {
                foreach (var serverAuthoritativeEntityId in _entityServerAuthority.Keys)
                {
                    if (!_entityMovement.ContainsKey(serverAuthoritativeEntityId) || !_entityPosition.ContainsKey(serverAuthoritativeEntityId))
                    {
                        continue;
                    }

                    var positionX = _entityPosition[serverAuthoritativeEntityId].Item1;
                    var positionY = _entityPosition[serverAuthoritativeEntityId].Item2;
                    var velocityX = _entityMovement[serverAuthoritativeEntityId].Item1;
                    var velocityY = _entityMovement[serverAuthoritativeEntityId].Item2;

                    var targetX = positionX + velocityX * 0.1f; 
                    var targetY = positionY + velocityY * 0.1f; 

                    var message = new MoveEntityMessage() { EntityId = serverAuthoritativeEntityId, X = targetX, Y = targetY };
                    Broadcast(Protocol.StcMoveEntity, ref message);
                }
                
                Task.Delay(100).Wait();
            }
        }
    }
}
