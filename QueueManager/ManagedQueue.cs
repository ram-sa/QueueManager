using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace QueueManager
{
    /// <summary>
    /// An implementation of <see cref="ConcurrentQueue{T}"/> that provides removal and tracking of its items.
    /// These features increase the execution time of certain methods like <see cref="Enqueue(T)"/> and 
    /// <see cref="TryDequeue(out T)"/>. Furthermore, the execution order of operations requested during a 
    /// <see cref="TryRemove(T)"/> will not be guaranteed.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the queue.</typeparam>
    public class ManagedQueue<T> : ConcurrentQueue<T>
    {
        #region Fields

        /// <summary>
        /// Lock used when removing an item from the <see cref="ManagedQueue{T}"/>.
        /// </summary>
        private static readonly object _removalLock = new object();
        /// <summary>
        /// Boolean flag used to signal <see cref="_removalLock"/> usage.
        /// </summary>
        private bool _isAvailable = true;
        /// <summary>
        /// Registry of all the operations ran in the <see cref="ManagedQueue{T}"/>.
        /// </summary>
        private ConcurrentBag<Tuple<DateTime, Operation>> _operations;
        #endregion

        #region Properties

        /// <summary>
        /// The creation time of this <see cref="ManagedQueue{T}"/> instance.
        /// </summary>
        public DateTime CreationTime { get; } = DateTime.Now;
        /// <summary>
        /// How long this instance of <see cref="ManagedQueue{T}"/> has been running.
        /// </summary>
        public TimeSpan Uptime
        {
            get
            {
                return (DateTime.Now - CreationTime);
            }
        }
        /// <summary>
        /// A list of the operations ran in this <see cref="ManagedQueue{T}"/> instance, ordered by time.
        /// </summary>
        public List<Tuple<DateTime,Operation>> History
        {
            get
            {
                return _operations.OrderBy(t => t.Item1).ToList();
            }
        }
        /// <summary>
        /// Returns the time since the last Dequeue operation. If no dequeues were made,
        /// returns the time since the first enqueue. If the queue is empty, returns zero.
        /// </summary>
        public TimeSpan WaitingTime
        {
            get
            {
                if (!IsEmpty)
                {
                    var c = History
                    .Where(q => q.Item2 == Operation.Dequeue)
                    .OrderByDescending(q => q.Item1);

                    if (c.Count() > 0)
                    {
                        return DateTime.Now - c.First().Item1;
                    }
                    else
                    {
                        c = History
                        .Where(q => q.Item2 == Operation.Enqueue)
                        .OrderBy(q => q.Item1);

                        if (c.Count() > 0)
                        {
                            return DateTime.Now - c.First().Item1;
                        }
                    }
                }
                return TimeSpan.Zero;
            }
        }
        /// <summary>
        /// The growth rate of this <see cref="ManagedQueue{T}"/> instance, calculated by the following formula:
        /// ((E - R) / T) / (D/T), where E = Enqueues, D = Dequeues, R = Removals and T is a time delta.
        /// </summary>
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
        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new <see cref="ManagedQueue{T}"/> instance.
        /// </summary>
        /// <param name="autoReset">Flag indicating whether the queue should reset itself every  
        /// day, cleaning the <see cref="History"/> registry. If disabled,
        /// this can eventualy lead to an <see cref="OutOfMemoryException"/> in case the queue 
        /// stays up too long without <see cref="Reset"/> being called.</param>
        public ManagedQueue(bool autoReset = true)
        {
            _operations = new ConcurrentBag<Tuple<DateTime, Operation>>();

            if (autoReset)
                new Timer(TimeSpan.FromDays(1).TotalMilliseconds).Elapsed += (o, e) => { Reset(); };
        }
        #endregion

        #region Methods

        /// <summary>
        /// Adds an object to the end of the <see cref="ManagedQueue{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the end of the <see cref="ManagedQueue{T}"/>.
        /// The value can be a null reference (Nothing in Visual Basic) for reference types.</param>
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

        /// <summary>
        /// Tries to remove and return the object at the beginning of the <see cref="ManagedQueue{T}"/>.
        /// </summary>
        /// <param name="result">When this method returns, if the operation was successful, result contains the
        /// object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>True if an element was removed and returned from the beginning of the <see cref="ManagedQueue{T}"/>
        /// successfully; otherwise, false.</returns>
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

        /// <summary>
        /// Tries to remove the specified object from the <see cref="ManagedQueue{T}"/>.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <returns>True if the item was removed succesfully; otherwise, false.</returns>
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

        /// <summary>
        /// Clears the <see cref="History"/> registry.
        /// </summary>
        public void Reset()
        {
            _operations = new ConcurrentBag<Tuple<DateTime, Operation>>();
        }

        /// <summary>
        /// Evaluates if the objects provided are equal based on their specific data structure.
        /// </summary>
        /// <param name="x">The first object to be evaluated.</param>
        /// <param name="y">The second object to be evaluated.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        private bool Equals(T x, T y)
        {
            return EqualityComparer<T>.Default.Equals(x, y);
        }
        #endregion
    }
}
