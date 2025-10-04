using UnityEngine;

[CreateAssetMenu(fileName = "SimulationSettings", menuName = "VehicleDynamics/SimulationSettings")]
public class SimulationSettings : ScriptableObject
{
    public float simulationRate = 200f;
    public int solverIterations = 6;
    public float timeMultiplier = 1f;
    public int maxFrameRate = 200;
    public float ambientTemperature = 20f;
    public float airDensity = 1.225f;
    public float gravity = 9.81f;
}
