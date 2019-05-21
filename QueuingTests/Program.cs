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
            //t.GrowthRateTest();
            t.QueueingManagerTest();
            //t.LastDequeueTest();
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

        public void QueueingManagerTest()
        {
            try
            {
                QueuingManager<int, GenericClass> m = new QueuingManager<int, GenericClass>(indices:null);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                QueuingManager<int, GenericClass> m = new QueuingManager<int, GenericClass>(new int[] { });
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                QueuingManager<string, GenericClass> m = new QueuingManager<string, GenericClass>(new string[]{ null, null });
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            QueuingManager<int, GenericClass> manager = new QueuingManager<int, GenericClass>(
                new CleanUpPolicy(0.0007, Hour.H18, Minute.M30), 0, 1);

            for(int i = 0; i < 10; i++)
            {
                manager.Enqueue(0, new GenericClass());
                if (i % 2 == 0)
                {
                    manager.Enqueue(1, new GenericClass());
                }
            }

            manager.Enqueue(new GenericClass(), out int index);

            manager.TryDequeue(out GenericClass item);

            manager.Enqueue(3, new GenericClass());

            try
            {
                manager.TryDequeue(out item, 4, 5);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            manager.TryDequeue(3, out item);

            manager.TryDequeue(3, out item);

            Thread.Sleep(660000);

            manager.TryDequeue(out _);

            while (true)
            {
                Console.WriteLine("ping");
                Thread.Sleep(3000);
            }
        }

        public void GrowthRateTest()
        {
            var q = new ManagedQueue<GenericClass>();

            for(int i = 0; i < 375600; i++)
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
                //Thread.Sleep(500);
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

        public void LastDequeueTest()
        {
            var q = new ManagedQueue<GenericClass>();
            Console.WriteLine("---------Queue Created---------");
            Thread.Sleep(5000);
            Console.WriteLine(q.WaitingTime);
            q.Enqueue(new GenericClass());
            Thread.Sleep(5000);
            q.Enqueue(new GenericClass());
            Thread.Sleep(10000);
            Console.WriteLine(q.WaitingTime); //output should be ~15 seconds
            q.TryDequeue(out _);
            Thread.Sleep(5000);
            Console.WriteLine(q.WaitingTime); //output should be ~5 seconds
            Thread.Sleep(5000);
            q.TryDequeue(out _);
            Thread.Sleep(5000);
            Console.WriteLine(q.WaitingTime);//output should be ~5 seconds
        }
    }

    public class GenericClass
    {
        public int GenericID { get; set; } = 1323;
        public string GenericText { get; set; } = "I'm really generic.";
        public bool IsGeneric { get; set; } = true;
    }
}
