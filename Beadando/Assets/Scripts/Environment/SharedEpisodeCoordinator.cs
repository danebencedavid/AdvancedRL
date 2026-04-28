public static class SharedEpisodeCoordinator
{
    private static PredatorAgent predatorAgent;
    private static PreyAgent preyAgent;
    private static bool isEndingEpisode;

    public static void RegisterPredator(PredatorAgent predator)
    {
        predatorAgent = predator;
    }

    public static void RegisterPrey(PreyAgent prey)
    {
        preyAgent = prey;
    }

    public static void EndBecauseMissingTarget()
    {
        if (!TryBeginEpisodeEnd())
        {
            return;
        }

        predatorAgent?.CompleteSharedEpisode(false, false);
        preyAgent?.CompleteSharedEpisode(false, false, false);
        FinishEpisodeEnd();
    }

    public static void EndBecauseTimeout()
    {
        if (!TryBeginEpisodeEnd())
        {
            return;
        }

        predatorAgent?.CompleteSharedEpisode(false, false);
        preyAgent?.CompleteSharedEpisode(true, false, false);
        FinishEpisodeEnd();
    }

    public static void EndBecausePredatorOutOfBounds()
    {
        if (!TryBeginEpisodeEnd())
        {
            return;
        }

        predatorAgent?.CompleteSharedEpisode(false, true);
        preyAgent?.CompleteSharedEpisode(false, false, false);
        FinishEpisodeEnd();
    }

    public static void EndBecausePreyOutOfBounds()
    {
        if (!TryBeginEpisodeEnd())
        {
            return;
        }

        predatorAgent?.CompleteSharedEpisode(false, false);
        preyAgent?.CompleteSharedEpisode(false, false, true);
        FinishEpisodeEnd();
    }

    public static void EndBecausePredatorCaughtPrey()
    {
        if (!TryBeginEpisodeEnd())
        {
            return;
        }

        predatorAgent?.CompleteSharedEpisode(true, false);
        preyAgent?.CompleteSharedEpisode(false, true, false);
        FinishEpisodeEnd();
    }

    private static bool TryBeginEpisodeEnd()
    {
        if (isEndingEpisode)
        {
            return false;
        }

        isEndingEpisode = true;
        return true;
    }

    private static void FinishEpisodeEnd()
    {
        isEndingEpisode = false;
    }
}
