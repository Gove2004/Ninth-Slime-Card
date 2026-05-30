using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;

public class SaveManager : MonoSingleton<SaveManager>
{
    private const string SavePath = "player";

    public void Save()
    {
        if (GameCore.playerData != null)
        {
            SaveCore.Save(SavePath, GameCore.playerData);
        }
    }

    public PlayerData Load()
    {
        PlayerData data = SaveCore.LoadOrDefault<PlayerData>(SavePath);
        GameCore.SetPlayerData(data);
        return data;
    }
}
