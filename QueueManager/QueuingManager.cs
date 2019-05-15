using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace QueueManager
{
    public class QueuingManager<I, T>
    {
        private readonly ConcurrentDictionary<I, ManagedQueue<T>> _queues = new ConcurrentDictionary<I, ManagedQueue<T>>();

        public bool HasQueues => !_queues.IsEmpty;
        public bool IsEmpty
        {
            get { return _queues.Values.Select(q => q.IsEmpty).Count() == _queues.Count; }
        }

        public void Enqueue(T item, out I index)
        {

        }

        public void Enqueue(T item, I index)
        {
            if (!_queues.ContainsKey(index))
                _queues.TryAdd(index, new ManagedQueue<T>());

            _queues[index].Enqueue(item);
        }

        public T Dequeue()
        {

        }

        public T Dequeue(I[] indexes)
        {

        }

        public T Dequeue(I index)
        {
            _queues[index].TryDequeue(out T item);
            return item;
        }

        public bool TryRemove(T item)
        {
            bool removed = false;

            foreach (I index in _queues.Keys)
            {
                if (_queues[index].TryRemove(item))
                {
                    removed = true;
                }
            }

            return removed;
        }

        public bool TryRemove(T item, I index)
        {
            if (_queues.ContainsKey(index))
            {
                return _queues[index].TryRemove(item);
            }

            return false;
        }

        private I BalancedEnqueue(T item)
        {

        }

        private T BalancedDequeue(I[] candidates)
        {

        }
    }
}
