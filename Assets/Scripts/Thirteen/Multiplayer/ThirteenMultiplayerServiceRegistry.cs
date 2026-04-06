public static class ThirteenMultiplayerServiceRegistry
{
    private static IThirteenMultiplayerService service;

    public static IThirteenMultiplayerService GetService()
    {
        service ??= new ThirteenRelayMultiplayerService();
        return service;
    }

    public static void Reset()
    {
        service?.LeaveLobby();
        service = null;
    }
}
