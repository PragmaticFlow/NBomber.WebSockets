using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.IO;
using NBomber.Contracts;

namespace NBomber.WebSockets;

/// <summary>
/// Represents configuration settings for a WebSocket connection.
/// </summary>
public class WebSocketConfig
{
    /// <summary>
    /// Gets or sets the default buffer size (in bytes) for WebSocket communication.
    /// Default is 16 KB (16,384 bytes).
    /// </summary>
    public int DefaultBufferSize { get; set; } = 16384; // 16KB
}

/// <summary>
/// Provides a client for connecting to WebSocket services.
/// This class wraps the native .NET <see cref="ClientWebSocket"/> and handles sending,
/// receiving, and managing WebSocket communication with stream-based buffering and message queuing.
/// </summary>
public class WebSocket(WebSocketConfig config) : IDisposable
{
    private static readonly RecyclableMemoryStreamManager MsStreamManager = new();
    private readonly Channel<WebSocketResponse> _channel = Channel.CreateUnbounded<WebSocketResponse>();
    private readonly CancellationTokenSource _cts = new();
    private bool _isListenUpdates = false;
    private long _msgReceivedCount;
    
    /// <summary>
    /// Gets the underlying <see cref="ClientWebSocket"/> instance used for communication.
    /// </summary>
    public ClientWebSocket Client { get; } = new();
    
    /// <summary>
    /// Gets the total number of messages received by the client.
    /// </summary>
    public long MsgReceivedCount => _msgReceivedCount;

    private async Task StartListenOnUpdates()
    {
        if (!_isListenUpdates)
            _isListenUpdates = true;
        else
            return;
        
        while (!_cts.IsCancellationRequested)
        {
            var endOfMessage = false;
            var ms = MsStreamManager.GetStream();
            var msgType = WebSocketMessageType.Binary;
            
            try
            {
                while (!endOfMessage)
                {
                    var buffer = ms.GetMemory(config.DefaultBufferSize);
                    var message = await Client.ReceiveAsync(buffer, _cts.Token);

                    if (message.MessageType == WebSocketMessageType.Close)
                    {
                        _channel.Writer.TryWrite(new WebSocketResponse(ms, WebSocketMessageType.Close));
                        _cts.Cancel();
                    }

                    ms.Advance(message.Count);

                    endOfMessage = message.EndOfMessage;
                    msgType = message.MessageType;
                }
                
                Interlocked.Increment(ref _msgReceivedCount);
                _channel.Writer.TryWrite(new WebSocketResponse(ms, msgType));
            }
            catch
            {
                ms.Dispose();
                _cts.Cancel();
                throw;
            }
        }
    }

    /// <summary>
    /// Asynchronously connects to a WebSocket server using a string URL.
    /// </summary>
    /// <param name="url">The WebSocket server URL.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <exception cref="WebSocketException">Thrown on connection failure.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public async Task Connect(string url, CancellationToken cancellationToken = default)
    {
        await Client.ConnectAsync(new Uri(url), cancellationToken);
        _ = StartListenOnUpdates();
    }

    /// <summary>
    /// Asynchronously connects to a WebSocket server using a URI.
    /// </summary>
    /// <param name="uri">The URI of the WebSocket server.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <exception cref="WebSocketException">Thrown on connection failure.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public async Task Connect(Uri uri, CancellationToken cancellationToken = default)
    {
        await Client.ConnectAsync(uri, cancellationToken);
        _ = StartListenOnUpdates();
    }
    
    /// <summary>
    /// Sends a UTF-8 encoded text message over the WebSocket connection.
    /// </summary>
    /// <param name="text">The text to be sent. It will be encoded using UTF8 encoding.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="WebSocketException">Thrown on send failure.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
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
    /// Sends binary data over the WebSocket connection.
    /// </summary>
    /// <param name="payload">The binary payload.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="WebSocketException">Thrown on send failure.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public ValueTask Send(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
    }
    
    /// <summary>
    /// Asynchronously receives a WebSocket message. The returned <see cref="WebSocketResponse"/>
    /// should be disposed after usage to free the underlying stream.
    /// </summary>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>A task that returns a <see cref="WebSocketResponse"/>.</returns>
    /// <exception cref="WebSocketException">Thrown if the client is not listening or in an invalid state.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the receive is canceled.</exception>
    public async ValueTask<WebSocketResponse> Receive(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_cts.IsCancellationRequested)
                throw new WebSocketException($"The client is not listening. The client State: {Client.State}");
                
            var response = await _channel.Reader.ReadAsync(cancellationToken);
            return response;
        }
        catch (OperationCanceledException)
        {
            throw new IgnoreMeasurementException();
        }
    }
    
    /// <summary>
    /// Gracefully closes the WebSocket connection with the specified status.
    /// </summary>
    /// <param name="closeStatus">The close status. Default is NormalClosure.</param>
    /// <param name="cancellationToken">Token to signal cancellation.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    /// <exception cref="WebSocketException">Thrown on close failure.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public async Task Close(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, CancellationToken cancellationToken = default)
    {
        await Client.CloseAsync(closeStatus, null, cancellationToken);
        _cts.Cancel();
    }

    /// <summary>
    /// Disposes the WebSocket client and cancels ongoing operations.
    /// </summary>
    public void Dispose()
    {
        Client.Dispose();
        _cts.Cancel();
    }
}

/// <summary>
/// Represents a response message from the WebSocket.
/// The response contains message data and its type, and must be disposed to free resources.
/// </summary>
public readonly struct WebSocketResponse(RecyclableMemoryStream memoryStream, WebSocketMessageType messageType) : IDisposable
{
    /// <summary>
    /// Gets the message payload as a read-only byte memory block.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; } = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
    
    /// <summary>
    /// Gets the WebSocket message type (Text, Binary, or Close).
    /// </summary>
    public WebSocketMessageType MessageType { get; } = messageType;

    /// <summary>
    /// Releases the memory stream associated with this response.
    /// </summary>
    public void Dispose()
    {
        memoryStream.Dispose();
    }
}