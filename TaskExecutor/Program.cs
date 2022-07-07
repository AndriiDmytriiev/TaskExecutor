using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;
using Barrier = System.Threading.Barrier;
using Monitor = System.Threading.Monitor;
using IDisposable = System.IDisposable;
using TaskEnum = System.Collections.Generic.IEnumerable<System.Threading.Tasks.Task>;
using TaskQueue = System.Collections.Generic.Queue<System.Threading.Tasks.Task>;
using Enumerable = System.Linq.Enumerable;
using ObjectDisposedException = System.ObjectDisposedException;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using _Imported_Extensions_;
namespace _Imported_Extensions_
{
    public static class Extensions
    {
        public static bool Any(this TaskEnum te)
        {
            return Enumerable.Any(te);
        }

        public static TaskEnum ToList(this TaskEnum te)
        {
            return Enumerable.ToList(te);
        }
    }
}

namespace TaskUtils
{
    public class SameThreadTaskScheduler : System.Threading.Tasks.TaskScheduler, IDisposable
    {
        static void Main(string[] args)
        {
            /*var TaskScheduler = new SameThreadTaskScheduler("RunIt");
            TaskScheduler.StartThread("");
            TaskScheduler.GetScheduledTasks();*/
            // Create a scheduler that uses two threads. 
            var lcts = new SameThreadTaskScheduler("2");
            List<Task> tasks = new List<Task>();

            // Create a TaskFactory and pass it our custom scheduler. 
            TaskFactory factory = new TaskFactory(lcts);
            CancellationTokenSource cts = new CancellationTokenSource();

            // Use our factory to run a set of tasks. 
            Object lockObj = new Object();
            int outputItem = 0;

            for (int tCtr = 0; tCtr <= 4; tCtr++)
            {
                int iteration = tCtr;
                Task t = factory.StartNew(() => {
                    for (int i = 0; i < 1000; i++)
                    {
                        lock (lockObj)
                        {
                            Console.Write("{0} in task t-{1} on thread {2}   ",
                                          i, iteration, Thread.CurrentThread.ManagedThreadId);
                            outputItem++;
                            if (outputItem % 3 == 0)
                                Console.WriteLine();
                        }
                    }
                }, cts.Token);
                tasks.Add(t);
            }
            // Use it to run a second set of tasks.                       
            for (int tCtr = 0; tCtr <= 4; tCtr++)
            {
                int iteration = tCtr;
                Task t1 = factory.StartNew(() => {
                    for (int outer = 0; outer <= 10; outer++)
                    {
                        for (int i = 0; i <= 1; i++)
                        {
                            lock (lockObj)
                            {
                                Console.Write("'{0}' in task t1-{1} on thread {2}   ",
                                              Convert.ToChar(i), iteration, Thread.CurrentThread.ManagedThreadId);
                                outputItem++;
                                if (outputItem % 3 == 0)
                                    Console.WriteLine();
                            }
                        }
                    }
                }, cts.Token);
                tasks.Add(t1);
            }

            // Wait for the tasks to complete before displaying a completion message.
            Task.WaitAll(tasks.ToArray());
            cts.Dispose();
            Console.WriteLine("\n\nSuccessful completion.");
            Console.ReadKey();
        }

        #region publics
        public SameThreadTaskScheduler(string name)
        {
            scheduledTasks = new TaskQueue();
            threadName = name;
        }
        
        public override int MaximumConcurrencyLevel { get { return 1; } }
        public void Dispose()
        {
            lock (scheduledTasks)
            {
                quit = true;
                Monitor.PulseAll(scheduledTasks);
            }
        }
        #endregion

        #region protected overrides
        protected override TaskEnum GetScheduledTasks()
        {
            lock (scheduledTasks)
            {
                return scheduledTasks.ToList();
            }
        }
        protected override void QueueTask(Task task)
        {
            if (myThread == null)
                myThread = StartThread(threadName);
            if (!myThread.IsAlive)
                throw new ObjectDisposedException("My thread is not alive, so this object has been disposed!");
            lock (scheduledTasks)
            {
                scheduledTasks.Enqueue(task);
                Monitor.PulseAll(scheduledTasks);
            }
        }
        protected override bool TryExecuteTaskInline(Task task, bool task_was_previously_queued)
        {
            return false;
        }
        #endregion

        private readonly TaskQueue scheduledTasks;
        private Thread myThread;
        private readonly string threadName;
        private bool quit;

        private Thread StartThread(string name)
        {
            var t = new Thread(MyThread) { Name = name };
            using (var start = new Barrier(2))
            {
                t.Start(start);
                ReachBarrier(start);
            }
            return t;
        }
        private void MyThread(object o)
        {
            Task tsk;
            lock (scheduledTasks)
            {
                //When reaches the barrier, we know it holds the lock.
                //
                //So there is no Pulse call can trigger until
                //this thread starts to wait for signals.
                //
                //It is important not to call StartThread within a lock.
                //Otherwise, deadlock!
                ReachBarrier(o as Barrier);
                tsk = WaitAndDequeueTask();
            }
            for (;;)
            {
                if (tsk == null)
                    break;
                TryExecuteTask(tsk);
                lock (scheduledTasks)
                {
                    tsk = WaitAndDequeueTask();
                }
            }
        }
        private Task WaitAndDequeueTask()
        {
            while (!scheduledTasks.Any() && !quit)
                Monitor.Wait(scheduledTasks);
            return quit ? null : scheduledTasks.Dequeue();
        }

        private static void ReachBarrier(Barrier b)
        {
            if (b != null)
                b.SignalAndWait();
        }
    }
}



    
        
