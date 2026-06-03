using UnityEngine;

public class QuestNPCSpawner : MonoBehaviour
{
    private string _lastSpawnedMapId = "";

    private void Update()
    {
        if (MapManager.Instance == null || string.IsNullOrEmpty(MapManager.Instance.CurrentMapId)) 
            return;

        string currentMapId = MapManager.Instance.CurrentMapId;

        if (_lastSpawnedMapId != currentMapId)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            QuestNPC existingNpc = FindAnyObjectByType<QuestNPC>();

            if (player != null && existingNpc == null)
            {
                SpawnNPC(player, currentMapId);
            }
        }
    }

    private void SpawnNPC(PlayerController player, string mapId)
    {
        if (MapManager.Instance._PlayerPrefab == null)
        {
            return;
        }

        GameObject npcGo = Instantiate(MapManager.Instance._PlayerPrefab);
        npcGo.name = "QuestNPC_" + mapId;

        PlayerController pc = npcGo.GetComponent<PlayerController>();
        if (pc != null)
        {
            Destroy(pc);
        }

        npcGo.AddComponent<QuestNPC>();

        Vector3 playerPos = player.transform.position;
        Vector3 spawnOffset = new Vector3(5f, 0f, 6f); 
        
        npcGo.transform.position = playerPos + spawnOffset;
        npcGo.transform.rotation = player.transform.rotation;

        _lastSpawnedMapId = mapId;
        
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.InitializeQuestForMap(mapId);
        }

    }
}
