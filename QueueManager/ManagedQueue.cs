using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace QueueManager
{
    public class ManagedQueue<T> : ConcurrentQueue<T>
    {
        private bool _isAvailable;

        private static readonly object _removalLock = new object();
        private static readonly object _enqueueLock = new object();
        private static readonly object _dequeueLock = new object();

        public DateTime CreationDate { get; } = DateTime.Now;
        public TimeSpan Uptime
        {
            get
            {
                return (DateTime.Now - CreationDate);
            }
        }
        public DateTime LastEmpty { get; private set; }
        public DateTime LastDequeue { get; private set; }
        public TimeSpan IdleTime
        {
            get
            {
                return (DateTime.Now - LastDequeue);
            }
        }

        public new void Enqueue(T item)
        {
            if (!_isAvailable)
            {
                lock (_removalLock)
                {
                    base.Enqueue(item);
                }
            }
            base.Enqueue(item);
        }

        public new bool TryDequeue(out T result)
        {
            bool dequeued;
            DateTime emptyTime = LastEmpty;

            if (!_isAvailable)
            {
                lock (_removalLock)
                {
                    dequeued = base.TryDequeue(out result);
                    if (IsEmpty)
                        emptyTime = DateTime.Now;
                }
            }
            else
            {
                dequeued = base.TryDequeue(out result);
                if (IsEmpty)
                    emptyTime = DateTime.Now;
            }

            if (dequeued)
            {
                lock (_dequeueLock)
                {
                    LastEmpty = emptyTime;
                    LastDequeue = DateTime.Now;
                }
            }

            return dequeued;
        }

        public bool TryRemove(T item)
        {
            bool found = false;

            if (!IsEmpty)
            {
                lock (_enqueueLock)
                {
                    _isAvailable = false;

                    TryDequeue(out T first);
                    if (Equals(item, first))
                    {
                        found = true;
                        LastEmpty = DateTime.Now;
                    }
                    else
                    {
                        Enqueue(first);
                        TryPeek(out T peek);
                        while (!Equals(peek, first))
                        {
                            TryDequeue(out T temp);
                            if (Equals(temp, item))
                                found = true;
                            else
                                Enqueue(temp);
                            TryPeek(out peek);
                        }
                    }
                    _isAvailable = true;
                }
            }

            return found;
        }

        private bool Equals(T x, T y)
        {
            return EqualityComparer<T>.Default.Equals(x, y);
        }
    }
}
