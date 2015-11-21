using System;
using System.Threading;
using System.Threading.Tasks;

namespace AspNet.Caching.MongoDb
{
    internal sealed class AsyncLock {

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> _releaser;

        public AsyncLock() {
            _releaser = Task.FromResult<IDisposable>(new Releaser(this));
        }

        public Task<IDisposable> LockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            var wait = _semaphore.WaitAsync(cancellationToken);
            return wait.IsCompleted 
                ? _releaser 
                : wait.ContinueWith((_, state) => (IDisposable)state, _releaser.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private sealed class Releaser : IDisposable {
            private readonly AsyncLock _lockToRelease;

            internal Releaser(AsyncLock toRelease) {
                _lockToRelease = toRelease;
            }

            ~Releaser() {
                Dispose(false);
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing) {
                if (disposing) {
                    _lockToRelease._semaphore.Release();
                }
            }
        }
    }
}
