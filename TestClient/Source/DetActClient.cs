using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient.Source
{
    public class DetActClient : IDisposable
    {
        private static readonly int MESSAGE_SIZE = 1024 * 4;

        private ClientWebSocket _client;
        private CancellationTokenSource _cancel;

        private Action clientClosedAction;

        private bool disposedValue;

        public DetActClient() => (_cancel, _client) = (new(), new());

        public Task Connect(Uri uri, Action clientClosedAction)
        {
            this.clientClosedAction = clientClosedAction;
            return _client.ConnectAsync(uri, CancellationToken.None);
        }

        public async void StartListening(Action<DetActMessage> receiveMessage)
        {
            try {
                var buffer = new byte[MESSAGE_SIZE];
                var result = await _client.ReceiveAsync(buffer, _cancel.Token);

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
                    receiveMessage(DetActMessage.CreateFromJSON(message));

                    result = await _client.ReceiveAsync(buffer, _cancel.Token);

                    if(_cancel.Token.IsCancellationRequested)
                        break;
                }

                if(_client.State is WebSocketState.CloseReceived) {
                    await CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure,
                        statusDescription: "close handshake");
                }
            }
            catch(Exception) {
                clientClosedAction();
            }
        }

        public async void Send(DetActMessage message)
        {
            if(_client?.State is WebSocketState.Open && message is not null) {
                await _client.SendAsync(buffer: new ArraySegment<byte>(message.ConvertToByteArray()),
                          WebSocketMessageType.Text, endOfMessage: true, _cancel.Token);
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            statusDescription ??= "";

            if(_client is not null) {
                await _client.CloseAsync(closeStatus, statusDescription, CancellationToken.None);
                clientClosedAction();
            }
        }

        public async Task CloseAsync()
        {
            await CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure, statusDescription: "close handshake");
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue) {
                if(disposing) {
                    _cancel.Cancel();
                    _cancel = null;

                    _client.Dispose();
                    _client = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
