using UnityEngine;

public class SelectionManager : MonoBehaviour
{
	public static SelectionManager Instance { get; private set; }

	public int selectedVehicleIndex = 0;
	public int selectedTrackIndex = 0;

	void Awake()
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
