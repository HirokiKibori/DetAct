using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using DetAct.Data.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DetAct.Data.Services
{
    public class WebSocketEventArgs : EventArgs
    {
        public enum EventType { CONNECTED, DISCONNECTED }

        public string Category { get; set; }

        public string Name { get; set; }

        public EventType Type { get; set; }

        public WebSocketEventArgs(string category, string name, EventType type = EventType.CONNECTED) => (Category, Name, Type) = (category, name, type);
    }

    public class WebSocketService : DisposableService
    {
        private bool _disposedValue;
        private readonly ILogger _logger;
        private DirectoryWatcherService _directoryWatcherService;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DetActSession>> _sessions = new();

        public event EventHandler<WebSocketEventArgs> OnDetActSessionsUpdated;

        public ConcurrentDictionary<string, ConcurrentDictionary<string, DetActSession>> Sessions { get => _sessions; }

        public WebSocketService(ILoggerFactory loggerFactory, ILogger<WebSocketService> logger
            , IHostApplicationLifetime applicationLifetime, DirectoryWatcherService directoryWatcherService)
            : base(applicationLifetime)
        {
            DetActSession.Logger = loggerFactory.CreateLogger<DetActSession>();
            _logger = logger;

            _directoryWatcherService = directoryWatcherService;
            _directoryWatcherService.Initialize("*.btml");
        }

        public async Task AcceptWebSocketAsync(string category, string name, HttpContext httpContext)
        {
            var session = await httpContext.WebSockets.AcceptWebSocketAsync();

            try {
                DetActSession currentSession = new();

                if(!AddSession(category, name, currentSession)) {
                    await session.CloseAsync(closeStatus: WebSocketCloseStatus.PolicyViolation,
                        statusDescription: "uri already in use by an oter session", CancellationToken.None);

                    return;
                }

                currentSession.Init(httpContext, session);
                currentSession.Components.Add(new BehaviourTreeSessionComponent(parent: currentSession, _directoryWatcherService));

                OnDetActSessionsUpdated?.Invoke(this, new WebSocketEventArgs(category, name));

                await currentSession.Run();
                RemoveSession(category, name);
            }
            catch(Exception e) {
                if(session?.State is WebSocketState.Open)
                    await session.CloseAsync(closeStatus: WebSocketCloseStatus.InternalServerError,
                        statusDescription: e.Message, CancellationToken.None);

                RemoveSession(category, name);

                _logger.LogTrace(message: e.Message, args: (object[])null);
            }
        }

        private bool AddSession(string category, string name, DetActSession session)
        {
            if(!_sessions.ContainsKey(key: category)) {
                _ = _sessions.TryAdd(key: category, value: new());
            }

            if(_sessions.TryGetValue(key: category, value: out var sessions)) {
                if(sessions.ContainsKey(key: name))
                    return false;

                return sessions.TryAdd(key: name, value: session);
            }

            return false;
        }

        private void RemoveSession(string category, string name)
        {
            if(_sessions.TryGetValue(key: category, value: out var sessions))
                if(sessions.TryRemove(key: name, value: out DetActSession session)) {
                    if(sessions.IsEmpty)
                        _sessions.TryRemove(key: category, out _);

                    OnDetActSessionsUpdated?.Invoke(this, new WebSocketEventArgs(category, name, WebSocketEventArgs.EventType.DISCONNECTED));
                }
        }

        public bool ContainsSession(string category, string name)
        {
            if(_sessions.TryGetValue(key: category, value: out var sessions)) {
                return sessions.ContainsKey(key: name);
            }

            return false;
        }

        public DetActSession GetSession(string category, string name)
        {
            if(_sessions.TryGetValue(key: category, value: out var sessions))
                if(sessions.TryGetValue(key: name, value: out DetActSession session))
                    return session;

            return null;
        }

        protected async virtual void Dispose(bool disposing)
        {
            if(!_disposedValue) {
                _disposedValue = true;

                if(disposing) {
                    var sessions = _sessions.Values
                        .SelectMany(category => category.Values)
                        .ToList();

                    foreach(var session in sessions)
                        await session.CloseAsync();

                    _sessions.Clear();
                }
            }
        }

        public override void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
