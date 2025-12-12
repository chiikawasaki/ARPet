using UnityEngine;
using UnityEngine.UI;
using Meta.XR.MRUtilityKit;

public class MiniMapRenderer : MonoBehaviour
{
    [Header("MiniMap UI")]
    public RawImage miniMapImage;

    [Header("Mesh → 2D Converter")]
    public SceneMeshTo2D meshTo2D;

    [Header("Auto Update")]
    public bool autoRefresh = false;
    public float refreshInterval = 2.0f;
    float timer;

    Texture2D currentMap;

    void Start()
    {
        if (miniMapImage == null)
        {
            Debug.LogError("MiniMapRenderer: miniMapImage is not set!");
            return;
        }

        if (meshTo2D == null)
        {
            meshTo2D = GetComponent<SceneMeshTo2D>();
        }

        GenerateMiniMap();
    }

    void Update()
    {
        if (!autoRefresh) return;

        timer += Time.deltaTime;
        if (timer >= refreshInterval)
        {
            timer = 0f;
            GenerateMiniMap();
        }
    }

    public void GenerateMiniMap()
    {
        if (meshTo2D == null)
        {
            Debug.LogError("MiniMapRenderer: No SceneMeshTo2D script found.");
            return;
        }

        currentMap = meshTo2D.Generate();
        if (currentMap == null)
        {
            Debug.LogError("MiniMapRenderer: Failed to generate minimap.");
            return;
        }

        miniMapImage.texture = currentMap;
    }
}
