using System.Net.WebSockets;
using Microsoft.IO;

namespace NBomber.WebSockets;

public class WebSocketConfig
{
    public int DefaultBufferSize { get; set; } = 16384; // 16KB
}

/// <summary>
/// Provides a client for connecting to WebSocket services.
/// WebSocket type is a wrapper over native .NET <see cref="ClientWebSocket"/> type.
/// </summary>
public class WebSocket(WebSocketConfig config) : IDisposable
{
    private static readonly RecyclableMemoryStreamManager MsStreamManager = new();

    public ClientWebSocket Client { get; } = new();

    /// <summary>
    /// Asynchronously receives a WebSocket message from the connected WebSocket.
    /// This method returns <see cref="WebSocketResponse"/> that should be disposed of after usage.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public async ValueTask<WebSocketResponse> Receive(CancellationToken? cancellationToken = null)
    {
        var endOfMessage = false;
        var ms = MsStreamManager.GetStream();
        var msgType = WebSocketMessageType.Binary;

        try
        {
            while (!endOfMessage)
            {
                var buffer = ms.GetMemory(config.DefaultBufferSize);
                var message = await Client.ReceiveAsync(buffer, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                if (message.MessageType == WebSocketMessageType.Close)
                {
                    return new WebSocketResponse(ms, WebSocketMessageType.Close);
                }

                ms.Advance(message.Count);

                endOfMessage = message.EndOfMessage;
                msgType = message.MessageType;
            }

            return new WebSocketResponse(ms, msgType);
        }
        catch
        {
            ms.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}

public class WebSocketResponse(RecyclableMemoryStream memoryStream, WebSocketMessageType messageType) : IDisposable
{
    public ReadOnlyMemory<byte> Data { get; } = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
    public WebSocketMessageType MessageType { get; } = messageType;

    public void Dispose()
    {
        memoryStream.Dispose();
    }
}