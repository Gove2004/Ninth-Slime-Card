using System.Collections.Generic;

public class RunState
{
    public int currentLv = 1;
    public List<int> playerDeckIds = new();

    public static readonly List<int> StarterDeckIds = new() { 101, 101, 101, 102, 102, 103 };

    public void EnsureStarterDeck()
    {
        if (playerDeckIds == null || playerDeckIds.Count == 0)
        {
            playerDeckIds = new List<int>(StarterDeckIds);
        }
    }

    public void Reset()
    {
        currentLv = 1;
        playerDeckIds = new List<int>(StarterDeckIds);
    }
}
