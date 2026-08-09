// ExtensionJobQueue.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge
// Network thread enqueues; GameComponent drains on main thread.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CAP_ChatInteractive.Extension
{
    public sealed class ExtensionJob
    {
        public string RequestId;
        public string Method;
        public string Path;
        public string Body;
        public string DevViewer;
        public TaskCompletionSource<string> Completion;

        public ExtensionJob()
        {
            Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public static class ExtensionJobQueue
    {
        private static readonly ConcurrentQueue<ExtensionJob> Queue = new ConcurrentQueue<ExtensionJob>();
        private const int MaxPerTick = 8;
        private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(12);

        public static void Enqueue(ExtensionJob job)
        {
            if (job == null) return;
            Queue.Enqueue(job);
        }

        public static int PendingCount => Queue.Count;

        /// <summary>Call from GameComponent on main thread.</summary>
        public static void ProcessPending()
        {
            int n = 0;
            while (n < MaxPerTick && Queue.TryDequeue(out var job))
            {
                n++;
                if (job == null) continue;
                try
                {
                    string json = ExtensionRouter.Handle(job);
                    job.Completion.TrySetResult(json ?? ExtensionEnvelope.Fail("Empty", "No response"));
                }
                catch (Exception ex)
                {
                    job.Completion.TrySetResult(ExtensionEnvelope.Fail("HandlerError", ex.Message));
                }
            }
        }

        public static async Task<string> EnqueueAndWaitAsync(ExtensionJob job, CancellationToken ct = default)
        {
            Enqueue(job);
            using (ct.Register(() => job.Completion.TrySetCanceled()))
            {
                var delay = Task.Delay(JobTimeout, ct);
                var done = await Task.WhenAny(job.Completion.Task, delay).ConfigureAwait(false);
                if (done == job.Completion.Task)
                    return await job.Completion.Task.ConfigureAwait(false);
                return ExtensionEnvelope.Fail("Timeout", "RICS did not process the request in time (main thread busy or paused).");
            }
        }
    }
}
