using System.Net.WebSockets;
using System.Text;
using Microsoft.IO;

namespace NBomber.WebSockets;

public static class WebSocketExtensions
{
    private static readonly RecyclableMemoryStreamManager MsStreamManager = new();

    /// <summary>
    /// This method should be used to connect to a WebSocket server asynchronously.
    /// </summary>
    /// <param name="url">The URL of the WebSocket server to connect to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public static Task Connect(this WebSocket webSocket, string url, CancellationToken cancellationToken = default)
    {
        return webSocket.Client.ConnectAsync(new Uri(url), cancellationToken);
    }

    /// <summary>
    /// This method should be used to connect to a WebSocket server asynchronously.
    /// </summary>
    /// <param name="uri">The URI of the WebSocket server to connect to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public static Task Connect(this WebSocket webSocket, Uri uri, CancellationToken cancellationToken = default)
    {
        return webSocket.Client.ConnectAsync(uri, cancellationToken);
    }

    /// <summary>
    /// This method should be used to close WebSocket connection asynchronously.
    /// </summary>
    /// <param name="closeStatus">The WebSocket close status.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public static Task Close(this WebSocket webSocket, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, CancellationToken cancellationToken = default)
    {
        return webSocket.Client.CloseAsync(closeStatus, null, cancellationToken);
    }

    /// <summary>
    /// This method should be used to send data on WebSocket asynchronously.
    /// </summary>
    /// <param name="text">The text to be sent. It will be encoded using UTF8 encoding.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public static ValueTask Send(this WebSocket webSocket, string text, CancellationToken cancellationToken = default)
    {
        using var ms = MsStreamManager.GetStream();

        var buffer = ms.GetMemory(text.Length);
        var byteCount = Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Span);
        ms.Advance(byteCount);

        var msg = ms.GetBuffer().AsMemory(0, (int) ms.Length);

        return webSocket.Client.SendAsync(msg, WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// This method should be used to send data on WebSocket asynchronously.
    /// </summary>
    /// <param name="payload">The binary payload to be sent.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to propagate notification that the operation should be canceled.</param>
    /// <exception cref="WebSocketException">Thrown when an error occurs during WebSocket communication.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive operation is canceled by the provided cancellation token.</exception>
    public static ValueTask Send(this WebSocket webSocket, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        return webSocket.Client.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
    }
}