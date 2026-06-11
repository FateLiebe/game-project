using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameLoader : MonoBehaviour
{
    [Header("Map dau tien")]
    public string firstMapName = "Map_1";

    private IEnumerator Start()
    {
        AsyncOperation loadOp =
            SceneManager.LoadSceneAsync(
                firstMapName,
                LoadSceneMode.Additive
            );

        yield return loadOp;

        yield return null;

        MapSpawnPoint spawnPoint =
            FindFirstObjectByType<MapSpawnPoint>();

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (spawnPoint != null && player != null)
        {
            player.transform.position =
                spawnPoint.transform.position;

            Debug.Log("Da dua Player toi SpawnPoint");
        }
        else
        {
            Debug.LogError("Khong tim thay SpawnPoint hoac Player");
        }
    }
}