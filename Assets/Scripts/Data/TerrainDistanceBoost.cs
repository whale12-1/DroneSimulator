using UnityEngine;

public class TerrainDistanceBoost : MonoBehaviour
{
    public float detailDistance = 800f;

    void Start()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain != null)
        {
            terrain.detailObjectDistance = detailDistance;
        }
    }
}
