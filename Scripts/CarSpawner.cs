using UnityEngine;

public class CarSpawner : MonoBehaviour
{
	public GameObject[] carPrefabs;
	public string spawnPointName = "SpawnPoint";

	private void Start()
	{
		var sel = SelectionManager.Instance;
		if (sel == null)
		{
			Debug.LogWarning("SelectionManager not found. No car will be spawned.");
			return;
		}

		int idx = sel.selectedVehicleIndex;
		if (carPrefabs == null || idx < 0 || idx >= carPrefabs.Length)
		{
			Debug.LogError("Car prefab not configured for selected index: " + idx);
			return;
		}

		GameObject spawnPoint = GameObject.Find(spawnPointName);
		if (spawnPoint == null)
		{
			Debug.LogError("SpawnPoint GameObject not found in scene (name: " + spawnPointName + ").");
			return;
		}

		Instantiate(carPrefabs[idx], spawnPoint.transform.position, spawnPoint.transform.rotation);
	}
}
