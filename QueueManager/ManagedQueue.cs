using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QueueManager
{
    public class ManagedQueue<T> : ConcurrentQueue<T>
    {
        private static readonly object _removalLock = new object();
        private readonly ConcurrentBag<Tuple<DateTime, Operation>> _operations;

        private bool _isAvailable = true;

        public DateTime CreationDate { get; } = DateTime.Now;
        public TimeSpan Uptime
        {
            get
            {
                return (DateTime.Now - CreationDate);
            }
        }
        public List<Tuple<DateTime,Operation>> History
        {
            get
            {
                return _operations.OrderBy(t => t.Item1).ToList();
            }
        }
        public double GrowthRate
        {
            get
            {
                if (History.Where(i => i.Item2 == Operation.Dequeue).Count() > 0)
                {
                    double timespan = (DateTime.Now - History.FirstOrDefault().Item1).TotalMinutes;

                    int enqueues = History.Where(i => i.Item2 == Operation.Enqueue).Count();
                    int dequeues = History.Where(i => i.Item2 == Operation.Dequeue).Count();
                    int removals = History.Where(i => i.Item2 == Operation.Removal).Count();

                    return ((enqueues - removals) / timespan) / (dequeues / timespan);
                }
                return History.Where(i => i.Item2 == Operation.Enqueue).Count();
            }
        }

        public ManagedQueue()
        {
            _operations = new ConcurrentBag<Tuple<DateTime, Operation>>();
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
            else
            {
                base.Enqueue(item);
            }
            _operations.Add(new Tuple<DateTime, Operation>(DateTime.Now, Operation.Enqueue));
        }

        public new bool TryDequeue(out T result)
        {
            bool dequeued;

            if (!_isAvailable)
            {
                lock (_removalLock)
                {
                    if (dequeued = base.TryDequeue(out result))
                    {
                        _operations.Add(new Tuple<DateTime, Operation>(DateTime.Now, Operation.Dequeue));
                    }
                }
            }
            else
            {
                if (dequeued = base.TryDequeue(out result))
                {
                   _operations.Add(new Tuple<DateTime, Operation>(DateTime.Now, Operation.Dequeue));
                }
            }

            return dequeued;
        }

        public bool TryRemove(T item)
        {
            bool found = false;

            if (!IsEmpty)
            {
                lock (_removalLock)
                {
                    _isAvailable = false;

                    base.TryDequeue(out T first);
                    if (Equals(item, first))
                    {
                        found = true;
                        _operations.Add(new Tuple<DateTime, Operation>(DateTime.Now, Operation.Removal));
                    }
                    else
                    {
                        base.Enqueue(first);
                        TryPeek(out T peek);
                        while (!Equals(peek, first))
                        {
                            base.TryDequeue(out T temp);
                            if (Equals(temp, item))
                            {
                                found = true;
                                _operations.Add(new Tuple<DateTime, Operation>(DateTime.Now, Operation.Removal));
                            }
                            else
                            {
                                base.Enqueue(temp);
                            }
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
