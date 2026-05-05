using UnityEngine;

public class AtlasRaycastDetector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float tileSize = 0.2f; 
    [SerializeField] private int atlasSize = 5;      
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private LayerMask layerMask;    

    void Update()
    {
        PerformRaycast();
    }

    private void PerformRaycast()
{
    // Смещение начала луча вверх на 0.5 метра, чтобы он точно не застрял внутри пола
    Vector3 origin = transform.position + Vector3.up * 0.5f;
    Vector3 direction = Vector3.down; // Направление строго вниз
    
    Ray ray = new Ray(origin, direction);
    
    // Рисуем луч в окне Scene для отладки
    Debug.DrawRay(origin, direction * rayDistance, Color.yellow);

    // Используем маску, чтобы не попасть в самого себя (игрока/кубик)
    if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, layerMask))
    {
        Vector2 uv = hit.textureCoord;
        int blockID = GetBlockIDFromUV(uv);

        // Выводим инфо: ID блока и имя объекта под ногами
        Debug.Log($"<color=orange>[Floor Check]</color> ID: {blockID} | Object: {hit.collider.name}");
    }
}
    private int GetBlockIDFromUV(Vector2 uv)
    {
        int col = Mathf.FloorToInt(uv.x / tileSize);
        int row = Mathf.FloorToInt(uv.y / tileSize);

        col = Mathf.Clamp(col, 0, atlasSize - 1);
        row = Mathf.Clamp(row, 0, atlasSize - 1);

        return row * atlasSize + col;
    }
}
