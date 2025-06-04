using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
public class Simulation : MonoBehaviour
{
    [SerializeField] public float simulationRate = 500f; // Hz
    [SerializeField] public int maxFrameRate = 60; // Hz
    [SerializeField] public float ambientTemperature = 20f; // Celsius
    [SerializeField] public float airDensity = 1.225f; // kg/m^3
    [SerializeField] public float gravity = 9.81f; // m/s^2

        void Awake()
        {
            Time.fixedDeltaTime = 1f / simulationRate;
            Physics.gravity = new Vector3(0f, -gravity, 0f);
            Application.targetFrameRate = maxFrameRate;
        }
    }
}