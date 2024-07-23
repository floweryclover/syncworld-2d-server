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
                if (_context.World.TryGetPlayerPosition(_context.Id, out var position))
                {
                    var spawnEntityMessage = new SpawnEntityMessage() { EntityId = spawnedEntityId, X = position.Item1, Y = position.Item2 };
                    _context.WriteMessage(Protocol.StcSpawnPlayer, ref spawnEntityMessage);
                }
            }
            return true;
        }
    }
}
