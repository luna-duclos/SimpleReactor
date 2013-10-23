using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace SimpleReactor
{
    /// <summary>
    /// Service interface
    /// </summary>
    public interface IService
    {
        void Start();
        void Stop();
    }

    /// <summary>
    /// Pollable interface
    /// </summary>
    public interface IPollable
    {
        TimeSpan PollTimeSpan { get; }
        void Poll();
    }

    public class Reactor
    {
        /// <summary>
        /// Whether the reactor has started
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// A cancellation token to be able to request cancellation events
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        private ISet<IService> services;
        private IDictionary<long, ISet<IPollable>> pollableTimers;

        public Reactor()
        {
            CancellationToken = new CancellationToken();
            services = new HashSet<IService>();
        }

        /// <summary>
        /// Start main processing loop
        /// </summary>
        /// <remarks>
        /// This algorithm uses a sorted dictionary to determine what pollable needs to be ran next.
        /// This results in a O(log n) implementation instead of a typical O(n) when looping through each pollable
        /// </remarks>
        public void Start()
        {
            pollableTimers = new SortedDictionary<long, ISet<IPollable>>();
            Started = true;
            long currentTime = DateTime.UtcNow.Ticks;

            // Populate sorted dictionary with initial runtimes
            foreach(IService service in services)
            {
                service.Start();

                if(service is IPollable)
                {
                    IPollable pollable = service as IPollable;
                    Schedule(pollable, currentTime + pollable.PollTimeSpan.Ticks);
                }
                
            }

            while(!CancellationToken.IsCancellationRequested)
            {
                // See whats next
                var kvp = pollableTimers.First();
                pollableTimers.Remove(kvp);

                long nextTime = kvp.Key;
                ISet<IPollable> pollables = kvp.Value;

                // See how long we need to wait to run it, if at all
                currentTime = DateTime.UtcNow.Ticks;
                if (nextTime > currentTime)
                {
                    Thread.Sleep(TimeSpan.FromTicks(nextTime - currentTime));
                }

                // Poll the pollable, readd it to the sorted dictionary so we can continue
                while(pollables.Count > 0)
                {
                    IPollable pollable = pollables.First();
                    pollables.Remove(pollable);
                    pollable.Poll();
                    Schedule(pollable, nextTime + pollable.PollTimeSpan.Ticks);
                }
            }
        }

        private void Schedule(IPollable pollable, long tick)
        {
            ISet<IPollable> pollables;
            
            if(!pollableTimers.TryGetValue(tick, out pollables))
            {
                pollables = new HashSet<IPollable>();
                pollableTimers[tick] = pollables;
            }

            pollables.Add(pollable);
        }

        /// <summary>
        /// Register the service
        /// </summary>
        /// <param name="service">The service to register</param>
        public void Add(IService service)
        {
            services.Add(service);

            // If the service is a pollable, also do some bookkeeping to schedule the pollable when necesary
            if(service is IPollable && Started)
            {
                IPollable pollable = service as IPollable;
                Schedule(pollable, DateTime.UtcNow.Ticks + pollable.PollTimeSpan.Ticks);
            }
        }

        public void Remove(IService service)
        {
            services.Remove(service);

            // If the service is a pollable, also do some bookkeeping and remove the pollable from scheduling
            if(service is IPollable && Started)
            {
                IPollable pollable = service as IPollable;
                var item = pollableTimers.First(kvp => kvp.Value == pollable);
                pollableTimers.Remove(item);
            }
        }
    }
}
