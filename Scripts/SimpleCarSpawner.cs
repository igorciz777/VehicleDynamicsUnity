using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleCarSpawner : MonoBehaviour
{
    public TMP_Dropdown carDropdown;
    public Button spawnButton;
    public GameObject[] cars;
    public GameObject spawnCamera;

    private void Start()
    {
        spawnButton.onClick.AddListener(SpawnSelectedCar);
    }
    private void SpawnSelectedCar()
    {
        int selectedIndex = carDropdown.value;
        if (selectedIndex >= 0 && selectedIndex < cars.Length)
        {
            cars[selectedIndex].SetActive(true);
            spawnCamera.SetActive(false);
        }
        else
        {
            Debug.LogError("Selected index is out of range of the cars array.");
        }
    }
}
