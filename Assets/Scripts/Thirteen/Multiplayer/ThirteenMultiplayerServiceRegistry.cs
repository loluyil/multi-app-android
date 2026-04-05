public static class ThirteenMultiplayerServiceRegistry
{
    private static IThirteenMultiplayerService service;

    public static IThirteenMultiplayerService GetService()
    {
        service ??= new ThirteenMockMultiplayerService();
        return service;
    }

    public static void Reset()
    {
        service?.LeaveLobby();
        service = null;
    }
}
