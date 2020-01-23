using System;
using System.Diagnostics;
using System.Linq;
using App.Metrics.Counter;
using App.Metrics.Histogram;

namespace Prometheus.DotNetRuntime.StatsCollectors.Util
{
    /// <summary>
    /// Helps calculate the ratio of process resources consumed by some activity.
    /// </summary>
    public class Ratio
    {
        private readonly Func<TimeSpan> _getElapsedTime;
        private TimeSpan _lastProcessTime;
        private double _lastEventTotalMillieSeconds;

        internal Ratio(Func<TimeSpan> getElapsedTime)
        {
            _getElapsedTime = getElapsedTime;
            _lastProcessTime = _getElapsedTime();
        }
        
        /// <summary>
        /// Calculates the ratio of CPU time consumed by an activity. 
        /// </summary>
        /// <returns></returns>
        public static Ratio ProcessTotalCpu()
        {
            var p = Process.GetCurrentProcess();
            return new Ratio(() =>
            {
                p.Refresh();
                return p.TotalProcessorTime;
            });
        }
        
        /// <summary>
        /// Calculates the ratio of process time consumed by an activity.
        /// </summary>
        /// <returns></returns>
        public static Ratio ProcessTime()
        {
            var startTime = DateTime.UtcNow;
            return new Ratio(() => DateTime.UtcNow - startTime);
        }
        
        public double CalculateConsumedRatio(double eventsCpuTimeTotalMilliSeconds)
        {
            var currentProcessTime = _getElapsedTime();
            var consumedProcessTime = currentProcessTime - _lastProcessTime;
            var eventsConsumedTimeMilliSeconds = eventsCpuTimeTotalMilliSeconds - _lastEventTotalMillieSeconds;

            if (eventsConsumedTimeMilliSeconds < 0.0)
            {
                // In this case, the difference between our last observed events CPU time and the current events CPU time is negative.
                // This means that we are being passed a non-incrementing value (which the caller should not be doing).
                // Rather than throwing an exception which could jeopardize the stability of event collection, we'll return a zero
                // TODO re-visit this and consider how to notify the user this is occurring 
                return 0.0;
            }
            
            _lastProcessTime = currentProcessTime;
            _lastEventTotalMillieSeconds = eventsCpuTimeTotalMilliSeconds;
            
            if (consumedProcessTime == TimeSpan.Zero)
            {
                // Avoid divide by zero
                return 0.0;
            }
            else
            {
                // We want to avoid a situation where we could return more than 100%. This could occur
                // if a delay is introduced between events being published and processed.
                // TODO need to potentially discard old events?
                return Math.Min(1.0, eventsConsumedTimeMilliSeconds / consumedProcessTime.TotalMilliseconds);
            }
        }

        public double CalculateConsumedRatio(ICounter eventCpuConsumedTotalMilliSeconds)
        {
            return CalculateConsumedRatio(eventCpuConsumedTotalMilliSeconds
                .GetValueOrDefault().Count);            
        }
        
        public double CalculateConsumedRatio(IHistogram eventCpuConsumedSeconds)
        {
            return CalculateConsumedRatio(eventCpuConsumedSeconds.GetValueOrDefault().Sum);            
        }
    }
}