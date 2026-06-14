namespace SportsData.Producer.Config
{
    [Flags]
    public enum ProducerRole
    {
        Api = 1,
        Ingest = 2,
        Worker = 4,

        /// <summary>
        /// Hosts long-running, non-interruptible Hangfire jobs (currently:
        /// CompetitionStreamerBase). Runs on a non-KEDA-scaled K8s Deployment
        /// so KEDA scale-down events don't cancel in-flight streamer jobs
        /// mid-game. Listens exclusively on the Hangfire "daemon" queue.
        /// See docs/contest-finalization-reconcile-backstop.md.
        /// </summary>
        Daemon = 8,

        All = Api | Ingest | Worker | Daemon
    }
}
