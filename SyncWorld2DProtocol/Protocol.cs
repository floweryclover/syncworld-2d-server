namespace SyncWorld2DProtocol
{
    public class Protocol
    {
        public const int HeaderSize = 4; // 2바이트 메시지 번호, 2바이트 바디길이
        public const int MaxMessageSize = 1024;

        public const int StcHelloClient = 1;

        public const int CtsRequestJoin = 1;
    }
}
