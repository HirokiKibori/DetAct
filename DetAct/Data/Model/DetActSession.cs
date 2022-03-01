using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using DetAct.Data.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DetAct.Data.Services
{
    public class DetActSession
    {
        private static readonly int MESSAGE_SIZE = 1024 * 4;
        public static ILogger Logger { get; set; } = null;

        public WebSocket _socket;
        public CancellationTokenSource _cancel;

        public bool Running { get; private set; } = false;

        public string ConnectionID { get; private set; } = "";

        public IPAddress IP { get; private set; } = IPAddress.None;

        public int Port { get; private set; } = -1;

        public WebSocketState State { get => _socket?.State ?? WebSocketState.None; }

        public List<SessionComponent> Components = new();

        public DetActSession() { }

        public void Init(HttpContext httpContext, WebSocket webSocket)
        {
            _ = httpContext ?? throw new ArgumentNullException(paramName: nameof(httpContext));
            _ = webSocket ?? throw new ArgumentNullException(paramName: nameof(webSocket));

            ConnectionID = httpContext.Connection.Id;
            IP = httpContext.Connection.RemoteIpAddress;
            Port = httpContext.Connection.RemotePort;

            _cancel = new();
            _socket = webSocket;
        }

        public async Task Run()
        {
            if(Running)
                return;

            Running = true;
            await ListenAsync();
            Running = false;
        }

        private async Task ListenAsync()
        {
            var buffer = new byte[MESSAGE_SIZE];

            try {
                var result = await _socket.ReceiveAsync(buffer, _cancel.Token);

                while(!result.CloseStatus.HasValue) {
                    if(result.MessageType is not WebSocketMessageType.Text) {
                        await CloseAsync(closeStatus: WebSocketCloseStatus.InvalidMessageType, statusDescription: "only text-messages allowed");

                        return;
                    }

                    if(result.Count > MESSAGE_SIZE || result.EndOfMessage is false) {
                        await CloseAsync(closeStatus: WebSocketCloseStatus.MessageTooBig,
                            statusDescription: $"The maximal message - size is { MESSAGE_SIZE }. Splitted packages are not supported.");

                        return;
                    }

                    var message = Encoding.UTF8.GetString(bytes: buffer, index: 0, count: result.Count);
                    var parseMessage = ReceivedMessage(message);

                    if(parseMessage is not null) {
                        await CloseAsync(closeStatus: WebSocketCloseStatus.InvalidPayloadData,
                            statusDescription: $"only accept JSON-messages of type 'DetActMessage' -> {parseMessage[..Math.Min(42, parseMessage.Length)]} ...");

                        return;
                    }

                    result = await _socket.ReceiveAsync(buffer, _cancel.Token);

                    if(_cancel.Token.IsCancellationRequested)
                        break;
                }

                if(_socket.State is WebSocketState.CloseReceived)
                    await _socket.CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure,
                        statusDescription: "close handshake", CancellationToken.None);
            }
            catch(Exception e) {
                await CloseAsync(closeStatus: WebSocketCloseStatus.InternalServerError,
                    statusDescription: e.Message);

                Logger?.LogError(message: e.Message);
                Logger?.LogTrace(exception: e, message: e.Message);
            }
            finally {
                CleanUp();
            }
        }

        public async Task SendMessage(DetActMessage message)
        {
            if(_socket?.State is WebSocketState.Open) {
                await _socket.SendAsync(buffer: new ArraySegment<byte>(message.ConvertToByteArray()),
                          WebSocketMessageType.Text, endOfMessage: true, _cancel.Token);
            }
        }

        private string ReceivedMessage(string message)
        {
            try {
                var detActMessage = JsonSerializer.Deserialize<DetActMessage>(json: message);

                Logger?.LogTrace(detActMessage.ToString());

                Task.Run(() =>
                {
                    Components.ForEach(component => component.ReceiveMessage(message: detActMessage));
                });

                return null;
            }
            catch(RuntimeWrappedException e) {
                Logger?.LogDebug(message: e.Message);
                Logger?.LogTrace(exception: e, message: e.Message);

                return e.Message;
            }
            catch(Exception e) {
                Logger?.LogDebug(message: e.Message);
                Logger?.LogTrace(exception: e, message: e.Message);

                return e.Message;
            }
        }

        public async Task CloseAsync()
        {
            await CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure, statusDescription: "close handshake");
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            statusDescription ??= "";

            if(_socket.State is WebSocketState.Open)
                await _socket.CloseAsync(closeStatus, statusDescription, CancellationToken.None);

            Components.ForEach(component => component.SessionClosed());
        }

        private void CleanUp()
        {
            _cancel?.Cancel();
            _cancel?.Dispose();
            _cancel = null;

            _socket?.Dispose();
            _socket = null;

            Components.ForEach(component => component.Dispose());
            Components.Clear();

            ConnectionID = "";
            IP = IPAddress.None;
            Port = -1;
        }
    }
}