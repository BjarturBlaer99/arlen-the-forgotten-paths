using System;
using System.Collections.Generic;
using Arlen.Quests;
using UnityEngine;

namespace Arlen.Saving
{
    /// <summary>
    /// Everything a save file holds. Plain serializable data (JsonUtility), no Unity object references:
    /// items, quests and world objects are stored by their stable string ids.
    /// Bump <see cref="CurrentVersion"/> when the layout changes and migrate in <see cref="SaveFile"/>.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string sceneName;
        public string savedAtUtc;

        public PlayerSaveData player = new PlayerSaveData();
        public List<QuestSaveEntry> quests = new List<QuestSaveEntry>();
        public List<string> questFacts = new List<string>();
        public List<ItemSaveEntry> inventory = new List<ItemSaveEntry>();
        public EquipmentSaveData equipment = new EquipmentSaveData();
        public List<ContainerSaveData> containers = new List<ContainerSaveData>();
        public List<string> collectedPickups = new List<string>();
        public List<NpcSaveData> npcs = new List<NpcSaveData>();
        public List<string> activatedWaybells = new List<string>();

        public ContainerSaveData FindContainer(string id) => containers.Find(c => c.id == id);
        public NpcSaveData FindNpc(string id) => npcs.Find(n => n.id == id);
    }

    [Serializable]
    public class PlayerSaveData
    {
        public bool hasPosition;
        public Vector3 position;
        public float yaw;
        [Tooltip("Health as a fraction of max, so equipment changes to max health don't matter.")]
        public float healthFraction = 1f;
    }

    [Serializable]
    public class ItemSaveEntry
    {
        public string itemId;
        public int count;

        public ItemSaveEntry() { }
        public ItemSaveEntry(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }

    [Serializable]
    public class EquipmentSaveData
    {
        public bool hasData;
        public string weaponId;
        public string charmId;
    }

    [Serializable]
    public class ContainerSaveData
    {
        public string id;
        public List<ItemSaveEntry> items = new List<ItemSaveEntry>();
    }

    [Serializable]
    public class NpcSaveData
    {
        public string id;
        public bool hasMet;
        public int repeatIndex;
        public int conversationsHad;
        public List<int> playedQuestConversations = new List<int>();
    }
}
