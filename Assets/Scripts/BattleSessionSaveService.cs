using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class SavedCardData
{
    public string name;
    public string typeName;
    public ulong cost;
    public ulong value;
    public List<ulong> values = new List<ulong>();
    public int duration;
    public bool isStolenFromOpponent;
    public SavedCardData mirroredCard;
}

[Serializable]
public class SavedCharacterData
{
    public ulong health;
    public ulong mana;
    public ulong shiled;
    public ulong autoManaPerTurn;
    public bool isPlayerReady;
    public int jailedTurnsRemaining;
    public List<SavedCardData> cards = new List<SavedCardData>();
}

[Serializable]
public class SavedDotData
{
    public int sourceSide;
    public int targetSide;
    public int duration;
    public bool isStolenFromOpponent;
    public SavedCardData sourceCard = new SavedCardData();
}

[Serializable]
public class BattleSnapshotData
{
    public int difficultyLevel;
    public int currentTurn;
    public bool playerIsInTurn;
    public bool enemyIsInTurn;
    public SavedCharacterData player = new SavedCharacterData();
    public SavedCharacterData enemy = new SavedCharacterData();
    public List<SavedCardData> playerDeck = new List<SavedCardData>();
    public List<SavedCardData> enemyDeck = new List<SavedCardData>();
    public List<SavedDotData> playerDots = new List<SavedDotData>();
    public List<SavedDotData> enemyDots = new List<SavedDotData>();
    public int enemyPhase;
    public ulong enemyNextPhaseHealthThreshold;
    public ulong enemyScore;
    public int rougeDamageTier;
    public ulong currentTotalDamage;
    public ulong sacrificeBonus;
    public ulong laserPlayerBonusDamage;
    public ulong laserEnemyBonusDamage;
}

[Serializable]
public class BattleSaveSlotData
{
    public string slotId;
    public long updatedAtTicks;
    public string label;
    public BattleSnapshotData snapshot = new BattleSnapshotData();
}

[Serializable]
public class BattleSaveSlotCollection
{
    public List<BattleSaveSlotData> slots = new List<BattleSaveSlotData>();
}

public class BattleSessionSaveService : MonoBehaviour
{
    public static BattleSessionSaveService Instance { get; private set; }
    private const string SaveCollectionKey = "BattleSaveSlots";
    private const string SaveFileName = "battle_save_slots.json";
    private BattleSaveSlotCollection collection = new BattleSaveSlotCollection();
    private string currentSlotId;

    private static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LoadCollection();
    }

    public void SetCurrentSlotId(string slotId)
    {
        currentSlotId = slotId;
    }

    public string GetCurrentSlotId()
    {
        return currentSlotId;
    }

    public void ClearCurrentSlotReference()
    {
        currentSlotId = null;
    }

    public List<BattleSaveSlotData> GetSlotsSorted()
    {
        var result = new List<BattleSaveSlotData>(collection.slots);
        result.Sort((left, right) => right.updatedAtTicks.CompareTo(left.updatedAtTicks));
        return result;
    }

    public BattleSaveSlotData SaveOrUpdateCurrentBattle()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.player == null || BattleManager.Instance.enemy == null)
        {
            return null;
        }

        var snapshot = BattleManager.Instance.CaptureBattleSnapshot();
        if (snapshot == null) return null;

        BattleSaveSlotData slot = null;
        if (!string.IsNullOrEmpty(currentSlotId))
        {
            slot = FindSlot(currentSlotId);
        }

        if (slot == null)
        {
            slot = new BattleSaveSlotData
            {
                slotId = Guid.NewGuid().ToString("N"),
                label = $"存档 {collection.slots.Count + 1}"
            };
            collection.slots.Add(slot);
            currentSlotId = slot.slotId;
        }

        slot.updatedAtTicks = DateTime.UtcNow.Ticks;
        slot.snapshot = snapshot;
        SaveCollection();
        return slot;
    }

    public bool TryLoadSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return false;
        var slot = FindSlot(slotId);
        if (slot == null || slot.snapshot == null) return false;
        if (GameManager.Instance == null) return false;

        GameManager.Instance.StartBattleFromSave(slot.snapshot, slot.slotId);
        return true;
    }

    public void DeleteCurrentSlotIfAny()
    {
        if (string.IsNullOrEmpty(currentSlotId)) return;
        DeleteSlot(currentSlotId);
    }

    public bool DeleteSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return false;
        int index = -1;
        for (int i = 0; i < collection.slots.Count; i++)
        {
            if (collection.slots[i] != null && collection.slots[i].slotId == slotId)
            {
                index = i;
                break;
            }
        }

        if (index < 0) return false;
        collection.slots.RemoveAt(index);
        if (currentSlotId == slotId) currentSlotId = null;
        SaveCollection();
        return true;
    }

    private BattleSaveSlotData FindSlot(string slotId)
    {
        for (int i = 0; i < collection.slots.Count; i++)
        {
            var slot = collection.slots[i];
            if (slot != null && slot.slotId == slotId) return slot;
        }
        return null;
    }

    private void LoadCollection()
    {
        string json = string.Empty;
        string filePath = GetSaveFilePath();

        try
        {
            if (File.Exists(filePath))
            {
                json = File.ReadAllText(filePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取战斗存档文件失败: {exception.Message}");
        }

        if (string.IsNullOrEmpty(json))
        {
            json = PlayerPrefs.GetString(SaveCollectionKey, string.Empty);
        }

        if (string.IsNullOrEmpty(json))
        {
            collection = new BattleSaveSlotCollection();
            return;
        }

        BattleSaveSlotCollection loaded = null;
        try
        {
            loaded = JsonUtility.FromJson<BattleSaveSlotCollection>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"解析战斗存档失败: {exception.Message}");
        }

        collection = loaded ?? new BattleSaveSlotCollection();
        if (collection.slots == null) collection.slots = new List<BattleSaveSlotData>();
    }

    private void SaveCollection()
    {
        if (collection == null) collection = new BattleSaveSlotCollection();
        if (collection.slots == null) collection.slots = new List<BattleSaveSlotData>();
        string json = JsonUtility.ToJson(collection);
        string filePath = GetSaveFilePath();
        string tempPath = filePath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? Application.persistentDataPath);
            File.WriteAllText(tempPath, json);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempPath, filePath);
        }
        catch (Exception exception)
        {
            Debug.LogError($"写入战斗存档文件失败: {exception.Message}");

            try
            {
                PlayerPrefs.SetString(SaveCollectionKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception fallbackException)
            {
                Debug.LogError($"回退保存战斗存档到 PlayerPrefs 失败: {fallbackException.Message}");
            }
        }
    }
}
