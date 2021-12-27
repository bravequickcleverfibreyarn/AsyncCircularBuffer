using Software9119.Aid.Concurrency;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Software9119.AsyncCircularBuffer;

/// <summary>
/// Thread safe non-blocking non-rewritable circular buffer.
/// </summary>
public class AsyncCircularBuffer<T> : IDisposable
{
  int head;
  int tail;
  int count;

  readonly T[] buffer;

  bool disposed;

  readonly AutoResetEvent operationHandle;
  readonly AutoResetEvent waitingHandle;

  int waitingCount;

  public AsyncCircularBuffer (int size)
  {
    buffer = new T [size];

    operationHandle = new AutoResetEvent (true);
    waitingHandle = new AutoResetEvent (false);
  }

  ~AsyncCircularBuffer () => Dispose (false);

  public bool IsEmpty => Empty (Count);
  public bool IsFull => Full (Count);


  public int Size => buffer.Length;
  public int Count => Volatile.Read (ref count);


  static bool Empty (int count) => count == 0;
  bool Full (int count) => count == Size;


  async public Task<T> DequeueAsync ()
  {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    await StartQueueOperation ();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

    bool canReleaseWaiting;
    if (Empty (count))
    {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
      _ = await BlockQueueOperation ();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
      Thread.MemoryBarrier ();
      canReleaseWaiting = false;
    }
    else
      canReleaseWaiting = true;

    T dequeued = buffer[NextPosition (ref tail)];
    --count;

    FinalizeQueueOperation (canReleaseWaiting);

    return dequeued;
  }

  async public Task EnqueueAsync (T addee)
  {

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    await StartQueueOperation ();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

    bool canReleaseWaiting;
    if (Full (count))
    {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
      _ = await BlockQueueOperation ();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
      Thread.MemoryBarrier ();
      canReleaseWaiting = false;
    }
    else
      canReleaseWaiting = true;

    buffer [NextPosition (ref head)] = addee;
    ++count;

    FinalizeQueueOperation (canReleaseWaiting);
  }

  async Task StartQueueOperation ()
  {
    _ = await operationHandle.WaitOneAsync ().ConfigureAwait (false);
    Thread.MemoryBarrier ();
  }

  async Task<bool> BlockQueueOperation ()
  {
    ++waitingCount;
    Thread.MemoryBarrier ();

    ConfiguredTaskAwaitable<bool> waiting = waitingHandle.WaitOneAsync().ConfigureAwait(false);
    _ = operationHandle.Set ();
    return await waiting;
  }

  void FinalizeQueueOperation (bool canReleaseWaiting)
  {
    if (canReleaseWaiting && waitingCount > 0)
    {
      --waitingCount;
      Thread.MemoryBarrier ();

      _ = waitingHandle.Set ();
      return;
    }

    _ = operationHandle.Set ();
  }


  int NextPosition (ref int position) => position = (position + 1) % buffer.Length;



  public void Dispose ()
  {
    if (disposed)
      return;

    waitingHandle.Dispose ();
    operationHandle.Dispose ();

    Dispose (true);
    GC.SuppressFinalize (this);

    disposed = true;
  }

  virtual protected void Dispose (bool disposing) { }
}
