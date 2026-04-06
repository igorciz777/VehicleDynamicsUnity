using UnityEngine;

/// <summary>
/// Singleton manager tracking user selections for vehicle and track in persistent scene.
/// </summary>
public class SelectionManager : MonoBehaviour
{
	public static SelectionManager Instance { get; private set; }

	public int selectedVehicleIndex = 0;
	public int selectedTrackIndex = 0;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}
}
