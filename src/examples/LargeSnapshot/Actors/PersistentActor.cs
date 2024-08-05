using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Akka.Util;

namespace LargeSnapshot.Actors;

public class PersistentActor: ReceivePersistentActor, IWithTimers
{
    private const int MinData = 16 * 1024;
    private const int MaxDataSize = 24 * 1024 * 1024;
    private readonly MemoryStream _buffer;

    public PersistentActor(string persistenceId)
    {
        _buffer = new MemoryStream();
        var currentLength = 0;
        PersistenceId = persistenceId;

        var log = Context.GetLogger();
        
        Recover<SnapshotOffer>(offer =>
        {
            var bytes = (byte[]) offer.Snapshot;
            log.Info($"Snapshot recovered. Size: {bytes.Length}");
            currentLength = bytes.Length;
            _buffer.Write(bytes);
        });
        
        Command<SaveSnapshotSuccess>(_ =>
        {
            log.Info("Snapshot saved");
        });
        
        Command<string>(
            msg => msg is "send", 
            _ =>
            {
                if (currentLength >= MaxDataSize)
                {
                    var data = _buffer.ToArray();
                    log.Info($"Saving snapshot, size: {data.Length}");
                    SaveSnapshot(data);
                }
                else
                {
                    var nextLength = currentLength * 2;
                    if (nextLength == 0)
                        nextLength = MinData;
                    else if (nextLength > MaxDataSize)
                        nextLength = MaxDataSize;
                
                    var diff = nextLength - currentLength;
                    currentLength = nextLength;
                    var buffer = new byte[diff];
                    ThreadLocalRandom.Current.NextBytes(buffer);
                    _buffer.Write(buffer);
                    
                    buffer = _buffer.ToArray();
                    log.Info($"Saving snapshot, size: {buffer.Length}");
                    SaveSnapshot(buffer);
                }
                
            });
        
        Command<string>(
            msg => msg is "crash",
            _ => throw new Exception("CRASH!"));
    }
    
    public override string PersistenceId { get; }
    public ITimerScheduler Timers { get; set; } = null!;
    
    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("send-key", "send", TimeSpan.FromSeconds(10));
        Timers.StartSingleTimer("crash-key", "crash", TimeSpan.FromSeconds(35));
    }

    protected override void PostStop()
    {
        _buffer.Dispose();
    }
}