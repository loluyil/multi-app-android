public static class ThirteenMultiplayerServiceRegistry
{
    private static IThirteenMultiplayerService service;

    public static IThirteenMultiplayerService GetService()
    {
        service ??= CreateService();
        return service;
    }

    private static IThirteenMultiplayerService CreateService()
    {
#if PHOTON_UNITY_NETWORKING
        if (ThirteenPhotonConfig.Load().IsConfigured)
            return new ThirteenPhotonRealtimeService();
#endif
        return new ThirteenMockMultiplayerService();
    }

    public static void Reset()
    {
        service?.LeaveLobby();
        service = null;
    }
}
