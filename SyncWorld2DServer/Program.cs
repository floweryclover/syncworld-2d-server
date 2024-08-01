using System.Diagnostics;
using System.Net.Sockets;
using SyncWorld2DProtocol;
using SyncWorld2DServer;

var tcpListener = TcpListener.Create(31415);
tcpListener.Start(8);
uint newPlayerId = 0;
var newPlayerIdLock = new object();
var world = new World();

var gameLogicUpdateTask = Task.Run(
    () =>
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        long previousMilliseconds = stopwatch.ElapsedMilliseconds;
        while (true)
        {
            long currentMilliseconds = stopwatch.ElapsedMilliseconds;
            float deltaTime = (currentMilliseconds - previousMilliseconds) / 1000.0f;
            world.Update(deltaTime);

            Thread.Sleep(16);
        }
    });

while (true)
{
    var tcpClient = tcpListener.AcceptTcpClient();
    var _ = Task.Run(
        async () =>
        {
            uint playerId;
            lock (newPlayerIdLock)
            {
                playerId = newPlayerId;
                newPlayerId++;
            }
            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint}({playerId}) 접속");
            // World 참여
            var context = new Context(tcpClient, playerId, world);
            world.OnPlayerJoined(playerId, context);

            var cancellationTokenSource = new CancellationTokenSource();
            var networkStream = tcpClient.GetStream();
            var receivingTask = Task.Run(
                async () =>
                {
                    var justBuffer = new byte[1024];
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            await context.ReceiveAndHandle(cancellationTokenSource.Token);
                        }
                        catch (SocketException e)
                        {
                            if (e.ErrorCode != (int)SocketError.ConnectionReset)
                            {
                                Console.WriteLine($"소켓 에러: {e.Message}");
                            }
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"{playerId} 예외 발생: {e.Message}");
                            break;
                        }
                    }
                });

            var sendingTask = Task.Run(
                async () =>
                {
                    var headerSerializeBuffer = new byte[Protocol.HeaderSize];
                    var semaphoreSlim = new SemaphoreSlim(1, 1);

                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await context.SendFlush(cancellationTokenSource.Token);
                    }
                });
            await Task.WhenAny(receivingTask, sendingTask);
            cancellationTokenSource.Cancel();
            await Task.WhenAll(receivingTask, sendingTask);

            world.OnPlayerLeft(playerId);
            context.OnDisconnected();
            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint}({playerId}) 접속 종료");
            context.Dispose();
        });
}