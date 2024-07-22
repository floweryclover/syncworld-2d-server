using System.Net.Sockets;
using SyncWorld2DProtocol;

var tcpListener = TcpListener.Create(31415);
tcpListener.Start(8);

while (true)
{
    var tcpClient = tcpListener.AcceptTcpClient();
    var _ = Task.Run(
        async () =>
        {
            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint} 접속");
            var cancellationToken = new CancellationTokenSource();
            var networkStream = tcpClient.GetStream();
            var receivingTask = Task.Run(
                async () =>
                {
                    var justBuffer = new byte[1024];
                    var receiveRingBuffer = new RingBuffer(Protocol.MaxMessageSize * 1024);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var receivedSize = await networkStream.ReadAsync(receiveRingBuffer.WritableOnceArraySegment, cancellationToken.Token);
                            receiveRingBuffer.UpdateWritten(receivedSize);

                            if (receivedSize == 0)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException e)
                        {
                            break;
                        }
                        catch (SocketException e)
                        {
                            Console.WriteLine($"소켓 에러: {e.Message}");
                            break;
                        }
                    }
                });

            var sendingTask = Task.Run(
                async () =>
                {
                    var sendRingBuffer = new RingBuffer(Protocol.MaxMessageSize * 1024);
                    var headerSerializeBuffer = new byte[Protocol.HeaderSize];
                    var semaphoreSlim = new SemaphoreSlim(1, 1);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int readableTotalSize;
                        await semaphoreSlim.WaitAsync();
                        try
                        {
                            readableTotalSize = sendRingBuffer.ReadableTotalSize;
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }

                        if (readableTotalSize == 0)
                        {
                            await Task.Delay(500);
                            continue;
                        }

                        await semaphoreSlim.WaitAsync();
                        try
                        {
                            if (sendRingBuffer.ReadableTotalSize == 0)
                            {
                                continue;
                            }
                            try
                            {
                                await networkStream.WriteAsync(sendRingBuffer.ReadableOnceArraySegment, cancellationToken.Token);
                                sendRingBuffer.UpdateRead(sendRingBuffer.ReadableOnceSize);
                            }
                            catch (OperationCanceledException e)
                            {
                                break;
                            }
                            catch (SocketException e)
                            {
                                Console.WriteLine($"소켓 에러: {e.Message}");
                                break;
                            }
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }
                    }
                });
            await Task.WhenAny(receivingTask, sendingTask);
            cancellationToken.Cancel();
            await Task.WhenAll(receivingTask, sendingTask);

            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint} 접속 종료");
            tcpClient.Close();
        });
}