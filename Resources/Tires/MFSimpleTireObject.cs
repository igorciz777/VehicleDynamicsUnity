using UnityEngine;

[CreateAssetMenu(fileName = "MFSimpleTireObject", menuName = "Scriptable Objects/MFSimpleTireObject")]
public class MFSimpleTireObject : TireObject
{
    // Simplified Pacejka's Magic Formula Coefficients
    [Header("Stiffness factor")]
    public float B = 10f; // Stiffness factor
    [Header("Longitudinal Shape factor")]
    public float C_Long = 1.6f; // Longitudinal Shape factor
    [Header("Lateral Shape factor")]
    public float C_Lat = 1.35f;  // Lateral Shape factor
    [Header("Peak factor")]
    public float D = 1f;   // Peak factor
    [Header("Curvature factor")]
    public float E = 0.97f; // Curvature factor
}
