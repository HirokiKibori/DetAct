using System;

using Microsoft.Extensions.Hosting;

namespace DetAct.Data.Services
{
    public abstract class DisposableService : IDisposable
    {
        public DisposableService(IHostApplicationLifetime applicationLifetime)
        {
            applicationLifetime.ApplicationStopping.Register(() => Dispose());
        }

        public abstract void Dispose();
    }
}
