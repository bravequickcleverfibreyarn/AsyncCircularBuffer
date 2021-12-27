
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Software9119.AsyncCircularBuffer;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest;

[TestClass]
public class AsyncCircularBufferTests
{

  const TaskStatus
    ranToCompletion       = TaskStatus.RanToCompletion,
    waitingForActivation  = TaskStatus.WaitingForActivation;

  [TestMethod]
  async public Task IsFull_States_ExpectedBehavior ()
  {
    using AsyncCircularBuffer<uint> buffer = new (5);

    Assert.IsFalse (buffer.IsFull);

    for (uint i = 1u; i < 6u; ++i)
      await buffer.EnqueueAsync (i);

    Assert.IsTrue (buffer.IsFull);

    _ = await buffer.DequeueAsync ();

    Assert.IsFalse (buffer.IsFull);
  }

  [TestMethod]
  async public Task IsEmpty_States_ExpectedBehavior ()
  {
    using AsyncCircularBuffer<uint> buffer = new (5);

    Assert.IsTrue (buffer.IsEmpty);

    await buffer.EnqueueAsync (3u);

    Assert.IsFalse (buffer.IsEmpty);

    _ = await buffer.DequeueAsync ();

    Assert.IsTrue (buffer.IsEmpty);
  }

  [TestMethod]
  public void Size_SizeEquals ()
  {
    const int size = 5;

    using AsyncCircularBuffer<uint> buffer = new (size);
    Assert.AreEqual (size, buffer.Size);
  }

  [TestMethod]
  async public Task Count_States_ExpectedBehavior ()
  {
    using AsyncCircularBuffer<int> buffer = new (5);

    Assert.AreEqual (0, buffer.Count);

    const int
        inclusiveStart = 1,
        exclusiveEnd = 6;

    for (int i = inclusiveStart; i < exclusiveEnd; ++i)
    {
      await buffer.EnqueueAsync (i);
      Assert.AreEqual (i, buffer.Count);
    }

    for (int i = inclusiveStart; i < exclusiveEnd; ++i)
    {
      _ = await buffer.DequeueAsync ();
      Assert.AreEqual (5 - i, buffer.Count);
    }
  }

  [TestMethod]
  async public Task EnqueAsyncDequeAsync_SomeItems_DequedAsExpected ()
  {
    using AsyncCircularBuffer<uint> buffer = new (5);

    for (uint i = 1u; i < 3u; ++i)
      await buffer.EnqueueAsync (i);

    Assert.AreEqual (1u, await buffer.DequeueAsync ());
    Assert.AreEqual (2u, await buffer.DequeueAsync ());
  }

  [TestMethod]
  async public Task EnqueAsync_FullQueue_NonBlockingResponseBehavior ()
  {
    using AsyncCircularBuffer<uint> buffer = new (5);

    const uint awaitedTaskValue = 6u;

    Task task = null;
    for (uint i = 1u; i < 7u; ++i)
    {
      task = buffer.EnqueueAsync (i);
      Thread.Sleep (400);

      TaskStatus status = i == awaitedTaskValue
            ? waitingForActivation
            : ranToCompletion;

      Assert.AreEqual (status, task.Status);
    }

    Assert.AreEqual (1u, await buffer.DequeueAsync ());
    Thread.Sleep (300);
    Assert.AreEqual (ranToCompletion, task.Status); // Enque-blocked finishes when it can.

    for (uint i = 2u; i < 7u; ++i)
      Assert.AreEqual (i, await buffer.DequeueAsync ());

    task = buffer.DequeueAsync ();
    Thread.Sleep (400);
    Assert.AreEqual (waitingForActivation, task.Status); // No more Items. Deque is awaited.
  }

  [TestMethod]
  async public Task EnqueAsync_FullQueueMoreRequests_NonBlockingResponseBehavior ()
  {
    const uint
        firstSet__inclusiveStart  = 1u,
        firstSet__exclusiveEnd    = 6u;

    using AsyncCircularBuffer<uint> buffer = new ((int)(firstSet__exclusiveEnd - firstSet__inclusiveStart));

    for (uint i = firstSet__inclusiveStart; i < firstSet__exclusiveEnd; ++i)
      await buffer.EnqueueAsync (i);

    const uint
        secondSet__inclusiveStart = firstSet__exclusiveEnd,
        secondSet__exclusiveEnd   = 11u;

    List<Task> enquedTasks = new ((int)(secondSet__exclusiveEnd - secondSet__inclusiveStart));

    for (uint i = secondSet__inclusiveStart; i < secondSet__exclusiveEnd; ++i)
    {
      Task task = buffer.EnqueueAsync(i);
      Thread.Sleep (400);
      Assert.AreEqual (waitingForActivation, task.Status);

      enquedTasks.Add (task);
    }

    for (uint i = firstSet__inclusiveStart; i < firstSet__exclusiveEnd; ++i)
    {
      Assert.AreEqual (i, await buffer.DequeueAsync ());
      Thread.Sleep (100);

      Assert.AreEqual (ranToCompletion, enquedTasks [(int) i - 1].Status);
    }

    for (uint i = secondSet__inclusiveStart; i < secondSet__exclusiveEnd; ++i)
      Assert.AreEqual (i, await buffer.DequeueAsync ());

    Task<uint> request = buffer.DequeueAsync();
    Thread.Sleep (400);
    Assert.AreEqual (waitingForActivation, request.Status); // No more Items. Deque is awaited.    
  }

  [TestMethod]
  async public Task DequeAsync_EmptyQueue_NonBlockingResponseBehavior ()
  {
    using AsyncCircularBuffer<uint> buffer = new (5);

    Task<uint> task = buffer.DequeueAsync();
    Thread.Sleep (400);
    Assert.AreEqual (waitingForActivation, task.Status);

    const uint value = 1u;
    await buffer.EnqueueAsync (value);
    Thread.Sleep (100);

    Assert.AreEqual (ranToCompletion, task.Status);
    Assert.AreEqual (value, task.Result);
  }

  [TestMethod]
  async public Task DequeAsync_EmptyQueueMoreRequests_NonBlockingResponseBehavior ()
  {
    using AsyncCircularBuffer<int> buffer = new (5);

    Task<int> dequeRequest1 = GetRequest();
    Task<int> dequeRequest2 = GetRequest();
    Task<int> dequeRequest3 = GetRequest();
    Task<int> dequeRequest4 = GetRequest();

    Task<int> GetRequest ()
    {
      Task<int> request = buffer.DequeueAsync();
      Thread.Sleep (400);
      Assert.AreEqual (waitingForActivation, request.Status);

      return request;
    }

    await AddValue (-1, dequeRequest1);
    await AddValue (-2, dequeRequest2);
    await AddValue (-3, dequeRequest3);
    await AddValue (-4, dequeRequest4);

    async Task AddValue ( int val, Task<int> request )
    {
      await buffer.EnqueueAsync (val);

      Thread.Sleep (100);

      Assert.AreEqual (ranToCompletion, request.Status);
      Assert.AreEqual (val, request.Result);
    }
  }
}
