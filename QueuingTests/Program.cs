using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QueueManager;

namespace QueuingTests
{
    class Program
    {
        static void Main(string[] args)
        {
            TestClass t = new TestClass();
            //t.RemovalTest();
            //t.ParallelEnqueuesTest();
            //t.ParallelRemovalTest();
            //t.ParallelEnqueueTest();
            t.GrowthRateTest();
        }

    }

    public class TestClass
    {
        ManagedQueue<GenericClass> _queue = new ManagedQueue<GenericClass>();
        GenericClass _generic = new GenericClass();

        public TestClass()
        {
            for (int i = 0; i < 99997; i++)
            {
                _queue.Enqueue(new GenericClass());
            }
            
            _queue.Enqueue(_generic);
            _queue.Enqueue(new GenericClass());
            _queue.Enqueue(new GenericClass());
            var e = _queue.History;
        }

        public void GrowthRateTest()
        {
            var q = new ManagedQueue<GenericClass>();

            for(int i = 0; i < 100000; i++)
            {
                switch(new Random().Next(1, 3))
                {
                    case 1:
                        q.Enqueue(new GenericClass());
                        break;
                    case 2:
                        q.TryDequeue(out _);
                        break;
                }
                Console.WriteLine(q.GrowthRate);
                Thread.Sleep(500);
            }
        }

        public void ParallelRemovalTest()
        {
            List<GenericClass> list = _queue.ToList();
            _queue.Clear();
            Task t2 = Task.Factory.StartNew(() => list.AsParallel().ForAll(i => _queue.Enqueue(i)));
            Task t1 = Task.Factory.StartNew(() => list.AsParallel().ForAll(i => _queue.TryRemove(i)));

            Task.WaitAll(t1, t2);
        }

        public void ParallelEnqueuesTest()
        {
            Console.WriteLine(_queue.History.Count);
            List<GenericClass> list = _queue.ToList();
            var q = new ConcurrentQueue<GenericClass>();
            _queue.Clear();
            list.ForEach(i => _queue.Enqueue(i));
            Console.WriteLine(_queue.History.Count);
            list.ForEach(i => q.Enqueue(i));
            _queue.Clear();
            q.Clear();
            list.AsParallel().ForAll(i => _queue.Enqueue(i));
            Console.WriteLine(_queue.History.Count);
            list.AsParallel().ForAll(i => q.Enqueue(i));
            list.AsParallel().ForAll(i => _queue.TryDequeue( out _));
            Console.WriteLine(_queue.History.Count);
            list.AsParallel().ForAll(i => q.TryDequeue(out _));
        }

        public void ParallelEnqueueTest()
        {
            var list = _queue.ToList();
            _queue.Clear();
            list.AsParallel().ForAll(i => _queue.Enqueue(i));
        }

        public void RemovalTest()
        {
            _queue.TryRemove(_generic);
            _queue.Clear();
            var c = new GenericClass();
            _queue.Enqueue(c);
            _queue.TryRemove(c);
            _queue.TryRemove(c);
        }
    }

    public class GenericClass
    {
        public int GenericID { get; set; } = 1323;
        public string GenericText { get; set; } = "I'm really generic.";
        public bool IsGeneric { get; set; } = true;
    }
}
