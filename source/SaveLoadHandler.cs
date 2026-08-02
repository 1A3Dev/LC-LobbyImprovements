namespace LobbyImprovements
{
    internal static class SaveLoadHandler
    {
        internal static string SaveFileName = "LobbyImprovements.es3";

        internal static void Save<T>(string key, T value)
        {
            ES3.Save(key, value, SaveFileName);
        }

        internal static T Load<T>(string key, T defaultValue)
        {
            return ES3.Load(key, SaveFileName, defaultValue);
        }
    }
}