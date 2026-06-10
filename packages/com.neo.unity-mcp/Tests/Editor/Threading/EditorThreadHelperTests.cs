using System;
using System.Threading;
using System.Threading.Tasks;
using Neo.UnityMcp.Threading;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Threading
{
    public sealed class EditorThreadHelperTests
    {
        private EditorThreadHelper _helper;

        [SetUp]
        public void SetUp() => _helper = new EditorThreadHelper(); // constructed on the test (main) thread

        [TearDown]
        public void TearDown() => _helper.Dispose();

        [Test]
        public void IsMainThread_OnConstructingThread_IsTrue()
        {
            Assert.That(_helper.IsMainThread, Is.True);
        }

        [Test]
        public void ExecuteOnEditorThread_OnMainThread_RunsSynchronously()
        {
            var task = _helper.ExecuteOnEditorThreadAsync(() => 42);

            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result, Is.EqualTo(42));
        }

        [Test]
        public void ExecuteOnEditorThread_ThrowingFunc_FaultsTask()
        {
            var task = _helper.ExecuteOnEditorThreadAsync<int>(() => throw new InvalidOperationException("boom"));

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception?.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ExecuteAsync_OnMainThread_ReturnsResult()
        {
            var task = _helper.ExecuteAsyncOnEditorThreadAsync(() => Task.FromResult(9));

            Assert.That(task.Wait(2000), Is.True);
            Assert.That(task.Result, Is.EqualTo(9));
        }

        [Test]
        public void ExecuteOnEditorThread_FromBackgroundThread_QueuedUntilPumped()
        {
            // Enqueue from a background thread (so the synchronous main-thread fast-path is NOT taken).
            var queued = EnqueueFromBackground(() => 7);

            Assert.That(queued.IsCompleted, Is.False, "should wait for the editor pump");

            _helper.ProcessQueue(); // drain on the main (test) thread

            Assert.That(queued.Wait(2000), Is.True);
            Assert.That(queued.Result, Is.EqualTo(7));
        }

        [Test]
        public void Dispose_CancelsPendingAndSubsequentWork()
        {
            var pending = EnqueueFromBackground(() => 1);

            _helper.Dispose(); // drains; the pending item sees disposed and cancels

            Assert.That(pending.IsCanceled, Is.True);

            var afterDispose = _helper.ExecuteOnEditorThreadAsync(() => 2);
            Assert.That(afterDispose.IsCanceled, Is.True);
        }

        // Calls ExecuteOnEditorThreadAsync from a real background thread and returns the (queued) Task
        // without unwrapping it (Task.Run would unwrap and block on the pump).
        private Task<int> EnqueueFromBackground(Func<int> func)
        {
            Task<int> queued = null;
            var thread = new Thread(() => queued = _helper.ExecuteOnEditorThreadAsync(func));
            thread.Start();
            thread.Join();
            return queued;
        }
    }
}
