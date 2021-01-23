
using Software9119.Aid.Concurrency;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Software9119.AsyncCircularBuffer
{
  public class AsyncCircularBuffer<T> : IDisposable
  {
    int head;
    int tail;
    int count;

    readonly T[] buffer;

    volatile bool disposed;

    readonly AutoResetEvent process;
    readonly AutoResetEvent waiter;

    int waiterCount;

    public AsyncCircularBuffer(int size)
    {
      buffer = new T[size];

      process = new AutoResetEvent(true);
      waiter = new AutoResetEvent(false);
    }

    ~AsyncCircularBuffer() => Dispose(false);

    public bool IsEmpty => Count == 0;
    public bool IsFull => Count == buffer.Length;

    public int Size => buffer.Length;

    public int Count => Volatile.Read(ref count);

    public async Task<T> DequeueAsync()
    {
      await process.WaitOneAsync();

      bool releaseWaiter;
      if (IsEmpty)
      {
        await ProcessOperationBlock();
        releaseWaiter = false;
      }
      else
      {
        releaseWaiter = true;
      }

      T dequeued = buffer[NextPosition(ref tail)];

      Interlocked.Decrement(ref count);
      FinalizeQueueOperation(releaseWaiter);

      return dequeued;

    }

    public async Task EnqueueAsync(T addee)
    {
      await process.WaitOneAsync();

      bool releaseWaiter;
      if (IsFull)
      {
        await ProcessOperationBlock();
        releaseWaiter = false;
      }
      else
      {
        releaseWaiter = true;
      }

      buffer[NextPosition(ref head)] = addee;

      Interlocked.Increment(ref count);
      FinalizeQueueOperation(releaseWaiter);
    }


    Task<bool> ProcessOperationBlock()
    {
      Interlocked.Increment(ref waiterCount);
      Task<bool> waiting = waiter.WaitOneAsync();
      process.Set();
      return waiting;
    }

    void FinalizeQueueOperation(bool releaseWaiter)
    {
      if (releaseWaiter && Volatile.Read(ref waiterCount) > 0)
      {
        Interlocked.Decrement(ref waiterCount);
        waiter.Set();
        return;
      }

      process.Set();
    }

    int NextPosition(ref int position)
    {
      int newPosition = (Volatile.Read(ref position) + 1) % buffer.Length;
      Interlocked.Exchange(ref position, newPosition);

      return newPosition;
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    virtual protected void Dispose(bool disposing)
    {
      if (disposed)
      {
        return;
      }

      if (disposing)
      {
        waiter.Dispose();
        process.Dispose();
      }

      disposed = true;
    }
  }
}
