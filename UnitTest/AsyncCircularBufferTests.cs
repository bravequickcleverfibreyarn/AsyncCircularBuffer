
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Software9119.AsyncCircularBuffer;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest
{
  [TestClass]
  public class AsyncCircularBufferTests
  {
    [TestMethod]
    public void IsFull_States_ExpectedBehavior()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      Assert.IsFalse(buffer.IsFull);

      const uint
        inclusiveStart = 1u,
        exclusiveEnd = 6u;

      for (uint i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        buffer.EnqueueAsync(i).GetAwaiter().GetResult();
      }

      Assert.IsTrue(buffer.IsFull);

      for (uint i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        buffer.DequeueAsync().GetAwaiter().GetResult();
      }

      Assert.IsFalse(buffer.IsFull);

      buffer.Dispose();
    }

    [TestMethod]
    public void IsEmpty_States_ExpectedBehavior()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      Assert.IsTrue(buffer.IsEmpty);

      buffer.EnqueueAsync(3u).GetAwaiter().GetResult();

      Assert.IsFalse(buffer.IsEmpty);

      buffer.DequeueAsync().GetAwaiter().GetResult();

      Assert.IsTrue(buffer.IsEmpty);

      buffer.Dispose();
    }

    [TestMethod]
    public void Size_SizeEquals()
    {
      int size = 5;

      var buffer = new AsyncCircularBuffer<uint>(size);
      Assert.AreEqual(size, buffer.Size);

      buffer.Dispose();
    }

    [TestMethod]
    public void Count_States_ExpectedBehavior()
    {
      var buffer = new AsyncCircularBuffer<int>(5);

      Assert.AreEqual(0, buffer.Count);

      const int
        inclusiveStart = 1,
        exclusiveEnd = 6;

      for (int i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        buffer.EnqueueAsync(i).GetAwaiter().GetResult();
        Assert.AreEqual(i, buffer.Count);
      }

      for (int i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        buffer.DequeueAsync().GetAwaiter().GetResult();
        Assert.AreEqual(5 - i, buffer.Count);
      }

      Assert.AreEqual(0, buffer.Count);

      buffer.Dispose();
    }

    [TestMethod]
    public void Count_SomeCount_CountEquals()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      for (uint i = 1u; i < 5u; ++i)
      {
        buffer.EnqueueAsync(i).GetAwaiter().GetResult();
      }

      Assert.AreEqual(4, buffer.Count);

      buffer.DequeueAsync().GetAwaiter().GetResult();
      buffer.DequeueAsync().GetAwaiter().GetResult();

      Assert.AreEqual(2, buffer.Count);

      buffer.Dispose();
    }

    [TestMethod]
    public void EnqueAsyncDequeAsync_SomeItems_DequedAsExpected()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      for (uint i = 1u; i < 3u; ++i)
      {
        buffer.EnqueueAsync(i).GetAwaiter().GetResult();
      }

      Assert.AreEqual(1u, buffer.DequeueAsync().Result);
      Assert.AreEqual(2u, buffer.DequeueAsync().Result);

      buffer.Dispose();
    }

    [TestMethod]
    public void EnqueAsync_FullQueue_NonBlockingResponseBehavior()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      const uint awaitedTaskValue = 6u;

      Task task = null;
      for (uint i = 1u; i < 7u; ++i)
      {
        task = buffer.EnqueueAsync(i);
        Thread.Sleep(400);

        TaskStatus status = i == awaitedTaskValue
            ? TaskStatus.WaitingForActivation
            : TaskStatus.RanToCompletion;

        Assert.AreEqual(status, task.Status);
      }

      Assert.AreEqual(1u, buffer.DequeueAsync().Result);

      Thread.Sleep(300);
      Assert.AreEqual(TaskStatus.RanToCompletion, task.Status); // Enque-blocked task should finished when it can.

      Assert.AreEqual(2u, buffer.DequeueAsync().Result);
      Assert.AreEqual(3u, buffer.DequeueAsync().Result);
      Assert.AreEqual(4u, buffer.DequeueAsync().Result);
      Assert.AreEqual(5u, buffer.DequeueAsync().Result);

      Assert.AreEqual(6u, buffer.DequeueAsync().Result);

      task = buffer.DequeueAsync();
      Thread.Sleep(400);
      Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status); // No more Items. Deque is awaited.

      buffer.Dispose();
    }

    [TestMethod]
    public void EnqueAsync_FullQueueMoreRequests_NonBlockingResponseBehavior()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      const uint
        inclusiveStart = 1u,
        exclusiveEnd = 6u;

      for (uint i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        buffer.EnqueueAsync(i).GetAwaiter().GetResult();
      }

      IReadOnlyList<Task> enquedTasks = new Task[]
      {
        GetRequest(6u),
        GetRequest(7u),
        GetRequest(8u),
        GetRequest(9u),
        GetRequest(10u),
      };

      Task GetRequest(uint val)
      {
        Task request = buffer.EnqueueAsync(val);
        Thread.Sleep(400);
        Assert.AreEqual(TaskStatus.WaitingForActivation, request.Status);

        return request;
      }

      for (uint i = inclusiveStart; i < exclusiveEnd; ++i)
      {
        Assert.AreEqual(i, buffer.DequeueAsync().GetAwaiter().GetResult());
        Thread.Sleep(100);

        Assert.AreEqual(TaskStatus.RanToCompletion, enquedTasks[(int)i - 1].Status);
      }

      for (uint i = 6u; i < 11u; ++i)
      {
        Assert.AreEqual(i, buffer.DequeueAsync().GetAwaiter().GetResult());
      }

      Task<uint> request = buffer.DequeueAsync();
      Thread.Sleep(400);
      Assert.AreEqual(TaskStatus.WaitingForActivation, request.Status); // No more Items. Deque is awaited.

      buffer.Dispose();
    }

    //[TestMethod]
    //public void EnqueAsync_FullQueueWithTimeout_NonBlockingResponseTimedOut()
    //{
    //  var buffer = new AsyncCircularBuffer<DateTime>(5);

    //  const uint
    //    inclusiveStart = 1u,
    //    exclusiveEnd = 6u;

    //  for (uint i = inclusiveStart; i < exclusiveEnd; ++i)
    //  {
    //    buffer.EnqueueAsync(DateTime.Now).GetAwaiter().GetResult();
    //  }

    //  Task response = buffer.EnqueueAsync(DateTime.Now, 1_000);
    //  Thread.Sleep(400);
    //  Assert.AreEqual(TaskStatus.WaitingForActivation, response.Status);

    //  response.ContinueWith
    //  (
    //    x => Assert.AreEqual(TaskStatus.RanToCompletion, x.Status)
    //  );
    //}

    [TestMethod]
    public void DequeAsync_EmptyQueue_NonBlockingResponseBehavior()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      Task<uint> task = buffer.DequeueAsync();
      Thread.Sleep(400);
      Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);

      const uint value = 1u;
      buffer.EnqueueAsync(value).GetAwaiter().GetResult();
      Thread.Sleep(100);

      Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
      Assert.AreEqual(value, task.Result);

      buffer.Dispose();
    }

    [TestMethod]
    public void DequeAsync_EmptyQueueMoreRequests_NonBlockingResponse()
    {
      var buffer = new AsyncCircularBuffer<uint>(5);

      Task<uint> dequeRequest1 = GetRequest();
      Task<uint> dequeRequest2 = GetRequest();
      Task<uint> dequeRequest3 = GetRequest();
      Task<uint> dequeRequest4 = GetRequest();

      Task<uint> GetRequest()
      {
        Task<uint> request = buffer.DequeueAsync();
        Thread.Sleep(400);
        Assert.AreEqual(TaskStatus.WaitingForActivation, request.Status);

        return request;
      }

      AddValue(1u, dequeRequest1);
      AddValue(2u, dequeRequest2);
      AddValue(3u, dequeRequest3);
      AddValue(4u, dequeRequest4);

      void AddValue(uint val, Task<uint> request)
      {
        buffer.EnqueueAsync(val).GetAwaiter().GetResult();

        Thread.Sleep(100);

        Assert.AreEqual(TaskStatus.RanToCompletion, request.Status);
        Assert.AreEqual(val, request.Result);
      }

      buffer.Dispose();
    }

    //[TestMethod]
    //public void DequeAsync_EmptyQueueWithTimeout_NonBlockingResponseTimedOut()
    //{
    //  var buffer = new AsyncCircularBuffer<string>(0);
    //  Task<string> response = buffer.DequeueAsync(1_000);
      
    //  Thread.Sleep(400);
    //  Assert.AreEqual(TaskStatus.WaitingForActivation, response.Status);

    //  response.ContinueWith
    //  (
    //    x =>
    //    {
    //      Assert.AreEqual(TaskStatus.RanToCompletion, x.Status);
    //      Assert.AreEqual(default(string), x.Result);
    //    }
    //  );
    //}
  }
}
