using SyncWorld2DProtocol;
using SyncWorld2DProtocol.Stc;

namespace SyncWorld2DServer
{
    internal class Handler : SyncWorld2DProtocol.Cts.ICtsHandler
    {
        private readonly Context _context;
        
        public Handler(Context context)
        {
            _context = context;
        }

        public bool OnRequestJoin()
        {
            if (_context.World.SpawnPlayerCharacter(_context.Id, out var spawnedEntityId))
            {
                if (_context.World.TryGetEntityPosition(spawnedEntityId, out var position))
                {
                    var possessEntityMessage = new PossessEntityMessage() { EntityId = spawnedEntityId };
                    _context.WriteMessage(Protocol.StcPossessEntity, ref possessEntityMessage);
                    return true;
                }
            }

            throw new InvalidOperationException($"{_context.Id} 의 RequestJoin()을 정상적으로 처리하지 못했습니다.");
        }

        public bool OnSendCurrentPosition(float positionX, float positionY, float velocityX, float velocityY, float accelerationX, float accelerationY)
        {
            _context.World.MovePlayerCharacter(_context.Id, positionX, positionY, velocityX, velocityY, accelerationX, accelerationY);
            return true;
        }
    }
}
