using System.Net.Sockets;

namespace SyncWorld2DServer
{
    internal class NetworkUtils
    {
        public static bool IsSocketConnected(Socket socket)
        {
            return !((socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0)) || !socket.Connected);
        }
    }
}
