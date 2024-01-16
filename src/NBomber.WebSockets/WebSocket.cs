using System.Net.WebSockets;
using System.Text;
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
    /// This method should be used to connect to a WebSocket server asynchronously.
    /// </summary>
    /// <param name="url">The URL of the WebSocket server to connect to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public Task Connect(string url, CancellationToken cancellationToken = default)
    {
        return Client.ConnectAsync(new Uri(url), cancellationToken);
    }

    /// <summary>
    /// This method should be used to connect to a WebSocket server asynchronously.
    /// </summary>
    /// <param name="uri">The URI of the WebSocket server to connect to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public Task Connect(Uri uri, CancellationToken cancellationToken = default)
    {
        return Client.ConnectAsync(uri, cancellationToken);
    }
    
    /// <summary>
    /// This method should be used to send data on WebSocket asynchronously.
    /// </summary>
    /// <param name="text">The text to be sent. It will be encoded using UTF8 encoding.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public ValueTask Send(string text, CancellationToken cancellationToken = default)
    {
        using var ms = MsStreamManager.GetStream();

        var buffer = ms.GetMemory(text.Length);
        var byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
        ms.Advance(byteCount);

        var msg = ms.GetBuffer().AsMemory(0, (int) ms.Length);

        return Client.SendAsync(msg, WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// This method should be used to send data on WebSocket asynchronously.
    /// </summary>
    /// <param name="payload">The binary payload to be sent.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public ValueTask Send(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
    }
    
    /// <summary>
    /// This method should be used to receive a WebSocket message from the connected WebSocket asynchronously. 
    /// This method returns <see cref="WebSocketResponse"/> that should be disposed of after usage.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public async ValueTask<WebSocketResponse> Receive(CancellationToken cancellationToken = default)
    {
        var endOfMessage = false;
        var ms = MsStreamManager.GetStream();
        var msgType = WebSocketMessageType.Binary;

        try
        {
            while (!endOfMessage)
            {
                var buffer = ms.GetMemory(config.DefaultBufferSize);
                var message = await Client.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

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
    
    /// <summary>
    /// This method should be used to close WebSocket connection asynchronously.
    /// </summary>
    /// <param name="closeStatus">The WebSocket close status.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public Task Close(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, CancellationToken cancellationToken = default)
    {
        return Client.CloseAsync(closeStatus, null, cancellationToken);
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}

/// <summary>
/// Represents a response from a WebSocket. After usage the instance should be disposed.
/// </summary>
public readonly struct WebSocketResponse(RecyclableMemoryStream memoryStream, WebSocketMessageType messageType) : IDisposable
{
    public ReadOnlyMemory<byte> Data { get; } = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
    public WebSocketMessageType MessageType { get; } = messageType;

    public void Dispose()
    {
        memoryStream.Dispose();
    }
}