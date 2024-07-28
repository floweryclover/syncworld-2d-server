namespace SyncWorld2DProtocol
{
    public class Protocol
    {
        public const int HeaderSize = 4; // 2바이트 메시지 번호, 2바이트 바디길이
        public const int MaxMessageSize = 1024;

        public const int StcSpawnEntity = 1;
        public const int StcDespawnEntity = 2;
        public const int StcPossessEntity = 3;
        public const int StcUnpossessEntity = 4;
        public const int StcMoveEntity = 5;
        public const int StcAssignEntityColor = 6;

        public const int CtsRequestJoin = 1;
        public const int CtsSendCurrentPosition = 2;
    }
}
