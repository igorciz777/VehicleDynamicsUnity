using UnityEngine;

[CreateAssetMenu(fileName = "TireObject", menuName = "Scriptable Objects/TireObject")]
public class TireObject : ScriptableObject
{
    [Header("General Tire Properties")]
    // public string tireName = "New Tire";
    public float tireMass = 10f; // Mass in kg
}