using System;
using System.Threading.Tasks;

using DetAct.Data.Services;

namespace DetAct.Data.Model
{
    public abstract class SessionComponent : IDisposable
    {
        private bool disposedValue;

        protected Func<DetActMessage, Task> SendMessage { get; }

        public Action ComponentChanged { get; set; }

        public SessionComponent(DetActSession parent) => SendMessage = parent.SendMessage;

        public abstract void SessionClosed();

        public abstract void ReceiveMessage(DetActMessage message);

        protected abstract void ManagedDisposeHook();

        protected virtual void UnManagedDisposeHook() { }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue) {
                if(disposing) {
                    ManagedDisposeHook();
                }

                UnManagedDisposeHook();

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
