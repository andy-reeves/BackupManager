// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="DailyTrigger.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackupManager;

/// <summary>
///     Utility class for triggering an event every 24 hours at a specified time of day
/// </summary>
public class DailyTrigger : IDisposable
{
    public DailyTrigger(DateTime startTime) : this(startTime.Hour, startTime.Minute, startTime.Second)
    {
    }

    /// <summary>
    ///     Initiator
    /// </summary>
    /// <param name="hour">The hour of the day to trigger</param>
    /// <param name="minute">The minute to trigger</param>
    /// <param name="second">The second to trigger</param>
    public DailyTrigger(int hour, int minute = 0, int second = 0)
    {
        Utils.Trace("DailyTrigger enter");

        TriggerHour = new TimeSpan(hour, minute, second);
        CancellationToken = new CancellationTokenSource();
        RunningTask = Task.Run(async () =>
        {
            while (true)
            {
                var triggerTime = DateTime.Today + TriggerHour - DateTime.Now;

                if (triggerTime < TimeSpan.Zero) triggerTime = triggerTime.Add(new TimeSpan(24, 0, 0));

                Utils.Trace($"triggerTime={triggerTime}");

                await Task.Delay(triggerTime, CancellationToken.Token);
                Utils.Trace("Invoke now");
                OnTimeTriggered?.Invoke();
                Utils.Trace("Invoke complete");
            }
        }, CancellationToken.Token);

        Utils.Trace("DailyTrigger exit");
    }

    /// <summary>
    ///     Time of day (from 00:00:00) to trigger
    /// </summary>
    public TimeSpan TriggerHour { get; }

    /// <summary>
    ///     Task cancellation token source to cancel delayed task on disposal
    /// </summary>
    private CancellationTokenSource CancellationToken { get; set; }

    /// <summary>
    ///     Reference to the running task
    /// </summary>
    private Task RunningTask { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        CancellationToken?.Cancel();
        CancellationToken?.Dispose();
        CancellationToken = null;
        RunningTask?.Dispose();
        RunningTask = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     The time left until the next trigger fires
    /// </summary>
    /// <returns></returns>
    public TimeSpan TimeToNextTrigger()
    {
        return Utils.TimeLeft(DateTime.Now, TriggerHour);
    }

    /// <summary>
    ///     Triggers once every 24 hours on the specified time
    /// </summary>
    public event Action OnTimeTriggered;

    /// <summary>
    ///     Finalized to ensure Dispose is called when out of scope
    /// </summary>
    ~DailyTrigger()
    {
        Dispose();
    }
}