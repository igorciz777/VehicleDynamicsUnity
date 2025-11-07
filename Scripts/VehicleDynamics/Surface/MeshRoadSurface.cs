using System;
using UnityEngine;

namespace VehicleDynamics
{
    public class MeshRoadSurface : MonoBehaviour
    {
        public String surfaceName;
        public float decayingFrictionCoefficient;
        public Vector2 frictionCoefficients; // x = longitudinal, y = lateral
        public float rollingResistanceCoefficient;
        public AudioClip surfaceSkidSound;
        public float surfaceSkidPitch = 1.0f;

        void Start()
        {
            if (!TryGetComponent<MeshCollider>(out var meshCollider))
            {
                Debug.LogError($"MeshRoadSurface on GameObject '{gameObject.name}' requires a MeshCollider component.");
            }
        }
    }
}
