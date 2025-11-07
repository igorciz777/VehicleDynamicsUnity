using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VehicleDynamics
{
    public class TerrainRoadSurface : MonoBehaviour
    {
        private Terrain terrain;
        public List<TerrainRoadLayer> terrainRoadLayersList = new();

        void Awake()
        {
            if (!TryGetComponent(out terrain))
            {
                Debug.LogError($"TerrainRoadSurface on GameObject '{gameObject.name}' requires a Terrain component.");
                return;
            }
        }
        [ContextMenu("Set Layer List From Terrain")]
        private void SetLayerListFromTerrain()
        {
            if(!terrain)
            {
                if (!TryGetComponent(out terrain))
                {
                    Debug.LogError($"TerrainRoadSurface on GameObject '{gameObject.name}' requires a Terrain component.");
                    return;
                }
            }
            terrainRoadLayersList = terrain.terrainData.terrainLayers
                .Select(layer => new TerrainRoadLayer
                {
                    terrainLayer = layer,
                    decayingFrictionCoefficient = 0.0f,
                    frictionCoefficients = new Vector2(1.0f, 1.0f),
                    surfaceSkidSound = null,
                    surfaceSkidPitch = 1.0f
                })
                .ToList();
        }

        public TerrainRoadLayer GetRoadLayerAtPosition(Vector3 point)
        {
            if (!terrain)
            {
                Debug.LogError($"TerrainRoadSurface on GameObject '{gameObject.name}' requires a Terrain component.");
                return null;
            }

            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;

            // Convert world position to terrain local position
            Vector3 localPos = point - terrainPosition;

            // Normalize local position to [0, 1] range
            float normalizedX = localPos.x / terrainData.size.x;
            float normalizedZ = localPos.z / terrainData.size.z;

            // Get the alpha map at the normalized position
            int mapX = Mathf.Clamp((int)(normalizedX * terrainData.alphamapWidth), 0, terrainData.alphamapWidth - 1);
            int mapZ = Mathf.Clamp((int)(normalizedZ * terrainData.alphamapHeight), 0, terrainData.alphamapHeight - 1);

            float[,,] alphaMap = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

            // Find the dominant layer
            int dominantLayerIndex = 0;
            float maxAlpha = 0f;

            for (int i = 0; i < terrainData.terrainLayers.Length; i++)
            {
                if (alphaMap[0, 0, i] > maxAlpha)
                {
                    maxAlpha = alphaMap[0, 0, i];
                    dominantLayerIndex = i;
                }
            }

            // Return the corresponding TerrainRoadLayer
            if (dominantLayerIndex < terrainRoadLayersList.Count)
            {
                return terrainRoadLayersList[dominantLayerIndex];
            }
            else
            {
                Debug.LogWarning($"Dominant layer index {dominantLayerIndex} exceeds the TerrainRoadLayer list count.");
                return null;
            }
        }
    }

    [System.Serializable]
    public class TerrainRoadLayer
    {
        public TerrainLayer terrainLayer;
        public float decayingFrictionCoefficient;
        public Vector2 frictionCoefficients; // x = longitudinal, y = lateral
        public float rollingResistanceCoefficient;
        public AudioClip surfaceSkidSound;
        public float surfaceSkidPitch = 1.0f;
    }
}
