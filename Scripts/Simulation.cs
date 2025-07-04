using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Simulation : MonoBehaviour
    {
        [SerializeField] public bool enableRealtimeSettings = true; // Enable or disable real-time settings
        [SerializeField] public float simulationRate = 500f; // Hz
        [Range(0.1f, 10f)]
        [SerializeField] public float timeMultiplier = 1f; // Multiplier for simulation speed
        [SerializeField] public int maxFrameRate = 60; // Hz
        [SerializeField] public float ambientTemperature = 20f; // Celsius
        [SerializeField] public float airDensity = 1.225f; // kg/m^3
        [SerializeField] public float gravity = 9.81f; // m/s^2

        void Awake()
        {
            Time.fixedDeltaTime = 1f / simulationRate;
            Physics.gravity = new Vector3(0f, -gravity, 0f);
            Application.targetFrameRate = maxFrameRate;
            Time.timeScale = timeMultiplier;
        }
        void FixedUpdate()
        {
            if(enableRealtimeSettings)
            {
                Time.fixedDeltaTime = 1f / simulationRate;
                Physics.gravity = new Vector3(0f, -gravity, 0f);
                Application.targetFrameRate = maxFrameRate;
                Time.timeScale = timeMultiplier;
            }
        }
    }
}