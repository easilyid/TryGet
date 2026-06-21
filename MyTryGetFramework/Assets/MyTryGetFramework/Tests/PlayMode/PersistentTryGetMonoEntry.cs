using TryGet.Async;
using TryGet.Unity;

namespace TryGet.Tests.PlayMode
{
    public sealed class PersistentTryGetMonoEntry : TryGetMonoEntry
    {
        public TrackingModule Module { get; } = new TrackingModule();
        public ITGTaskScheduler Scheduler { get; private set; }
        public bool HasHost => Host != null;

        protected override void Setup(IModuleSystem host)
        {
            host.Register<ITrackingModule>(Module);
            Scheduler = host.Get<ITGTaskScheduler>();
        }
    }
}
