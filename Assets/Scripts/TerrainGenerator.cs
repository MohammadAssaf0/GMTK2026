using UnityEngine;

public class DuneGenerator : MonoBehaviour
{


    [Header("Terrain Reference")]
    [SerializeField] private Terrain targetTerrain;

    [Header("Big Dunes Parameters")]
    [Tooltip("How stretched out the dunes are. Lower = larger, wider dunes.")]
    [SerializeField] private float duneScale = 3f;

    [Tooltip("Maximum height of the dunes")]
    [SerializeField] private float duneHeight = 20f;

    [Tooltip("How sharply pinched the crests are. Higher = sharper ridges.")]
    [SerializeField] private float duneRidgeSharpness = 2.5f;

    [Tooltip("scroll through the noise map")]
    [SerializeField] private Vector2 duneNoiseOffset;
    
    [Header("Small Dunes Parameters")]
    [Tooltip("How stretched out the dunes are. Lower = larger, wider dunes.")]
    [SerializeField] private float smallDuneScale = 3f;

    [Tooltip("Maximum height of the dunes")]
    [SerializeField] private float smallDuneHeight = 20f;

    [Tooltip("How sharply pinched the crests are. Higher = sharper ridges.")]
    [SerializeField] private float smallRidgeSharpness = 2.5f;

    [Tooltip("scroll through the noise map")]
    [SerializeField] private Vector2 smallDuneNoiseOffset;

    private bool _updatingError;
    
    // Unity calls this whenever a value changes in the Inspector
    private void OnValidate()
    {
        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            // We use delayCall to avoid Unity throwing errors about modifying TerrainData during serialization
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= GenerateSafe; // Prevent queued up calls from stacking
            UnityEditor.EditorApplication.delayCall += GenerateSafe;
            #endif
        }
        else if (!_updatingError)
        {
            _updatingError =  true;
            Debug.Log("can't update the terrain");
        }
    }

    #if UNITY_EDITOR
    private void GenerateSafe()
    {
        // Ensure the script hasn't been deleted before the delayed call runs
        if (this == null || targetTerrain == null) return;
        Generate();
    }
    #endif

    [ContextMenu("Generate Dunes")]
    public void Generate()
    {
        if (targetTerrain == null)
        {
            targetTerrain = GetComponent<Terrain>();
            if (targetTerrain == null)
            {
                Debug.LogError("No Terrain assigned! Please assign a terrain in the inspector.");
                return;
            }
        }

        TerrainData terrainData = targetTerrain.terrainData;
        int width = terrainData.heightmapResolution;
        int length = terrainData.heightmapResolution;
        float[,] heights = new float[width, length];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float xCoord = (float)x / width * smallDuneScale + smallDuneNoiseOffset.x;
                float yCoord = (float)y / length * smallDuneScale + smallDuneNoiseOffset.y;

                float p = Mathf.PerlinNoise(xCoord, yCoord);
                float ridge = 1f - Mathf.Abs(p * 2f - 1f);
                float duneHeight = Mathf.Pow(ridge, smallRidgeSharpness);

                heights[x, y] = duneHeight * (smallDuneHeight / terrainData.size.y);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }
}