using System.Net.Sockets;

Console.WriteLine("Hello");


var tcpListener = TcpListener.Create(31415);
tcpListener.Start(8);

while (true)
{
    using var tcpClient = tcpListener.AcceptTcpClient();
    Console.WriteLine(tcpClient.Client.RemoteEndPoint?.ToString());
}