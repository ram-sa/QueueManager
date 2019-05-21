using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Timers;

namespace QueueManager
{
    /// <summary>
    /// Class used to provide load balancing between consumer oriented queues.
    /// </summary>
    /// <typeparam name="I">The type of the elements used for queue indexing.</typeparam>
    /// <typeparam name="T">The type of the elements contained in the queues.</typeparam>
    public class QueuingManager<I, T> : IDisposable
    {
        #region Fields

        /// <summary>
        /// Timer used for scheduling a registry cleaning operation.
        /// </summary>
        private Timer _cleanSchedule;

        /// <summary>
        /// Collection of <see cref="ManagedQueue{T}"/> queues.
        /// </summary>
        private readonly ConcurrentDictionary<I, ManagedQueue<T>> _queues;
        #endregion

        #region Properties

        /// <summary>
        /// The indices of the queues currently being managed.
        /// </summary>
        public List<I> CurrentIndices
        {
            get { return _queues.Select(q => q.Key).ToList(); }
        }
        /// <summary>
        /// Returns true if all queues are empty; otherwise, false.
        /// </summary>
        public bool IsEmpty
        {
            get { return _queues.Values.Where(q => q.IsEmpty).Count() == _queues.Count; }
        }
        /// <summary>
        /// Maximum waiting time of the queues. Takes priority over the <see cref="ManagedQueue{T}.GrowthRate"/>
        /// </summary>
        public TimeSpan MaxWaitingTime { get; set; } = TimeSpan.FromMinutes(10);
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="QueuingManager{I, T}"/> using the default registry cleaning policy
        /// (queues will clean entries every 24 hours from creation time) and an initial set of indices.
        /// </summary>
        /// <param name="indices">The indices to be used for queue identification.</param>
        /// <exception cref="ArgumentException">Throws if no indices are passed or one of them is null.</exception>
        public QueuingManager(params I[] indices)
        {
            if (indices.Count() > 0)
            {
                _queues = new ConcurrentDictionary<I, ManagedQueue<T>>();

                foreach (I index in indices)
                {
                    _queues.TryAdd(index, new ManagedQueue<T>());
                }
            }
            else
            {
                throw new ArgumentException("Class initialization must contain at least one parameter.", "indices");
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref= "QueuingManager{I, T}"/> using a customized policy 
        /// and an initial set of indices.
        /// </summary>
        /// <param name="policy">The customized policy.</param>
        /// <param name="indices">The indices to be used for queue identification.</param>
        /// <exception cref="ArgumentException">Throws if no indices are passed or one of them is null.</exception>
        /// <exception cref="ArgumentNullException">Throws if <see cref="CleanUpPolicy"/> is null.</exception>
        public QueuingManager(CleanUpPolicy policy, params I[] indices)
        {
            if (indices.Count() == 0)
            {
                throw new ArgumentException("Class initialization must contain at least one parameter.", "indices");
            }
            else if(policy is null)
            {
                throw new ArgumentNullException("Class initialization must contain a valid policy.", "policy");
            }
            else
            {
                DateTime now = DateTime.Now;
                DateTime date = new DateTime(now.Year, now.Month, now.Day, policy.Hour, policy.Minutes, 0);
                if (date < now)
                    date.AddDays(1);
                TimeSpan span = date - now;

                Timer t = new Timer(span.TotalMilliseconds);
                t.Elapsed += (o, e) =>
                {
                    _queues.Keys.ToList().ForEach(k => _queues[k].Reset());

                    _cleanSchedule = new Timer(TimeSpan.FromDays(policy.Interval).TotalMilliseconds);
                    _cleanSchedule.Elapsed += (ob, ev) =>
                    {
                        _queues.Keys.ToList().ForEach(k => _queues[k].Reset());
                    };
                    _cleanSchedule.Start();
                    t.Stop();
                    t.Dispose();
                };

                _queues = new ConcurrentDictionary<I, ManagedQueue<T>>();

                foreach (I index in indices)
                {
                    _queues.TryAdd(index, new ManagedQueue<T>(false));
                }

                t.Start();
            }

        }
        #endregion

        #region Methods

        /// <summary>
        /// Attempts to add a new queue with the specified index to the managed collection.
        /// </summary>
        /// <param name="index">The index used for queue identification.</param>
        /// <returns>True if the queue was added, false if the index already exists.</returns>
        public bool TryAddQueue(I index) => _queues.TryAdd(index, new ManagedQueue<T>());

        /// <summary>
        /// Adds an object to the end of the managed queue with the lowest growth rate.
        /// </summary>
        /// <param name="item">The object to add to the end of one of the managed queues.</param>
        /// <param name="index">The index of the queue where the object was added.</param>
        public void Enqueue(T item, out I index)
        {
            var queue = _queues
                .Select(q => new Tuple<I, ManagedQueue<T>>(q.Key, q.Value))
                .OrderBy(q => q.Item2.GrowthRate)
                .FirstOrDefault();
            queue.Item2.Enqueue(item);
            index = queue.Item1;
        }

        /// <summary>
        /// Adds an object to the end of the specified managed queue.
        /// </summary>
        /// <param name="index">The index of the queue where the object will be added.</param>
        /// <param name="item">The object to add to the end of the queue.</param>
        public void Enqueue(I index, T item)
        {
            if (!_queues.ContainsKey(index))
                _queues.TryAdd(index, new ManagedQueue<T>());

            _queues[index].Enqueue(item);
        }

        /// <summary>
        /// Tries to remove and return the object at the beginning of the managed queue where 
        /// the queue's waiting time has gone over <see cref="MaxWaitingTime"/> or, if there aren't any,
        /// from the queue with the greatest growth rate.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, result contains the
        /// object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>True if an element was removed and returned from the beginning of the <see cref="ManagedQueue{T}"/>
        /// successfully; otherwise, false.</returns>
        public bool TryDequeue(out T item)
        {
            return BalancedDequeue(_queues.Keys.ToArray(), out item);
        }

        /// <summary>
        /// Tries to remove and return the object at the beginning of the managed queue where 
        /// the queue's waiting time has gone over <see cref="MaxWaitingTime"/> or, if there aren't any,
        /// from the queue with the greatest growth rate, while considering the indices provided.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, result contains the
        /// object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <param name="indices">The indices of the queues that will be considered for the operation.</param>
        /// <returns>True if an element was removed and returned from the beginning of the <see cref="ManagedQueue{T}"/>
        /// successfully; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if one or more of the indices provided aren't 
        /// in the managed collection.</exception>
        public bool TryDequeue(out T item, params I[] indices)
        {
            if (indices.Intersect(_queues.Keys.ToArray()).Count() == indices.Count())
                return BalancedDequeue(indices, out item);
            else
                throw new ArgumentOutOfRangeException(
                    "indices",
                    "One or more indices aren't being currently managed.");
        }

        /// <summary>
        /// Tries to remove and return the object at the beginning of the specified managed queue.
        /// </summary>
        /// <param name="index">The index of the queue.</param>
        /// <param name="item">When this method returns, if the operation was successful, result contains the
        /// object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>True if an element was removed and returned from the beginning of the<see cref="ManagedQueue{T}"/>
        /// successfully; otherwise, false.</returns>
        /// <exception cref="IndexOutOfRangeException">Throws when the index provided isn't part of the
        /// managed collection.</exception>
        public bool TryDequeue(I index, out T item)
        {
            if (_queues.ContainsKey(index))
            {
                return _queues[index].TryDequeue(out item);
            }
            item = default;
            return false;
        }

        /// <summary>
        /// Attemps to remove the object from all managed queues.
        /// </summary>
        /// <param name="item">The object to be removed.</param>
        /// <returns>True if the removal was succesful, otherwise, false.</returns>
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

        /// <summary>
        /// Attempts to remove the object from the specified managed queue.
        /// </summary>
        /// <param name="item">The object to be removed.</param>
        /// <param name="index">The index of the queue.</param>
        /// <returns>True if the removal was succesful, otherwise, false.</returns>
        /// <exception cref="IndexOutOfRangeException">Throws when the index provided isn't part of the
        /// managed collection.</exception>
        public bool TryRemove(T item, I index)
        {
            return _queues[index].TryRemove(item);
        }

        /// <summary>
        /// Disposes of the current instance, forcing a release of its allocated resources.
        /// </summary>
        public void Dispose()
        {
            _cleanSchedule.Stop();
            _cleanSchedule.Dispose();
        }

        /// <summary>
        /// Implementation of <see cref="TryDequeue(out T)"/> and <see cref="TryDequeue(out T, I[])"/>.
        /// </summary>
        private bool BalancedDequeue(I[] indices, out T item)
        {
            if (!IsEmpty)
            {
                var queue = _queues
                    .Where(q => indices.Contains(q.Key) && q.Value.WaitingTime > MaxWaitingTime)
                    .OrderByDescending(q => q.Value.WaitingTime)
                    .FirstOrDefault();

                if (queue.Value is null)
                {
                    queue = _queues
                        .Where(q => indices.Contains(q.Key))
                        .OrderByDescending(q => q.Value.GrowthRate)
                        .FirstOrDefault();
                }

                return queue.Value.TryDequeue(out item);
            }
            item = default;
            return false;
        }
        #endregion
    }
}
