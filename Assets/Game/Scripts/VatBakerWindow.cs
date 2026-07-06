#if UNITY_EDITOR 
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class VatBakerWindow : EditorWindow
{
    private GameObject targetPrefab;
    private List<AnimationClip> animationClips = new List<AnimationClip>();
    private int frameRate = 60; 

    private int listSize = 0;

    [MenuItem("Tools/VAT Texture Baker")]
    public static void ShowWindow()
    {
        GetWindow<VatBakerWindow>("VAT Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Настройки VAT запекания", EditorStyles.boldLabel);
        
        targetPrefab = (GameObject)EditorGUILayout.ObjectField("Префаб персонажа", targetPrefab, typeof(GameObject), true);
        frameRate = EditorGUILayout.IntField("Кадров в секунду (FPS)", frameRate);

        EditorGUILayout.Space();
        GUILayout.Label("Список анимаций (Animation Clips)", EditorStyles.boldLabel);

        // Ручное управление размером списка, работающее без ошибок сериализации
        listSize = EditorGUILayout.IntField("Количество клипов", listSize);
        
        // Подгоняем размер реального списка под введенное число
        while (animationClips.Count < listSize) animationClips.Add(null);
        while (animationClips.Count > listSize) animationClips.RemoveAt(animationClips.Count - 1);

        // Отрисовываем поля для каждого клипа
        for (int i = 0; i < animationClips.Count; i++)
        {
            animationClips[i] = (AnimationClip)EditorGUILayout.ObjectField($"Клип {i}", animationClips[i], typeof(AnimationClip), false);
        }

        EditorGUILayout.Space();

        // Кнопка теперь появится в любом случае, но нажать её можно только при заполненных данных
        EditorGUI.BeginDisabledGroup(targetPrefab == null || animationClips.Count == 0 || animationClips[0] == null);
        if (GUILayout.Button("Запечь текстуру анимаций", GUILayout.Height(30)))
        {
            BakeVatTexture();
        }
        EditorGUI.EndDisabledGroup();
    }

       private void BakeVatTexture()
    {
        SkinnedMeshRenderer smr = targetPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("На префабе не найден SkinnedMeshRenderer!");
            return;
        }

        Mesh mesh = smr.sharedMesh;
        int vertexCount = mesh.vertexCount;

        int totalFrames = 0;
        List<int> clipFrameCounts = new List<int>();
        
        foreach (var clip in animationClips)
        {
            if (clip == null) continue;
            int frames = Mathf.CeilToInt(clip.length * frameRate);
            totalFrames += frames;
            clipFrameCounts.Add(frames);
        }

        if (totalFrames == 0) return;

        Texture2D vatTexture = new Texture2D(vertexCount, totalFrames, TextureFormat.RGBAHalf, false, true);
        vatTexture.filterMode = FilterMode.Point;
        vatTexture.wrapMode = TextureWrapMode.Clamp;

        GameObject tempGO = Instantiate(targetPrefab);
        // Принудительно зануляем трансформ временного объекта, чтобы избежать смещения координат в мировое пространство
        tempGO.transform.position = Vector3.zero;
        tempGO.transform.rotation = Quaternion.identity;
        tempGO.transform.localScale = Vector3.one;

        SkinnedMeshRenderer tempSmr = tempGO.GetComponentInChildren<SkinnedMeshRenderer>();
        Mesh bakedMesh = new Mesh();

        int currentPixelY = 0;
        string logReport = "Лог запекания анимаций (для Shader Graph):\n";

        for (int c = 0; c < animationClips.Count; c++)
        {
            AnimationClip clip = animationClips[c];
            if (clip == null) continue;
            
            int framesInClip = clipFrameCounts[c];
            float startOffsetInTexture = (float)currentPixelY / totalFrames;
            float clipLengthInTexture = (float)framesInClip / totalFrames;
            logReport += $"[{clip.name}]: Оффсет в текстуре = {startOffsetInTexture:F4}, Длина в текстуре = {clipLengthInTexture:F4}\n";

            for (int f = 0; f < framesInClip; f++)
            {
                float normalizedTime = (framesInClip > 1) ? (float)f / (framesInClip - 1) : 0f;
                float timeInSeconds = normalizedTime * clip.length;

                // Насильно симулируем кадр
                clip.SampleAnimation(tempGO, timeInSeconds);
                
                // Выпекаем меш
                tempSmr.BakeMesh(bakedMesh);
                Vector3[] vertices = bakedMesh.vertices;

                // ИСПРАВЛЕНИЕ: Получаем локальную матрицу самого SkinnedMeshRenderer относительно корня префаба,
                // чтобы компенсировать любые скрытые повороты Blender-модели (например, те самые 90 градусов)
                Matrix4x4 localMatrix = tempSmr.transform.localToWorldMatrix;

                for (int v = 0; v < vertexCount; v++)
                {
                    // Переводим вершину деформированного меша и оригинального меша в единую систему координат
                    Vector3 animatedLocalPos = localMatrix.MultiplyPoint3x4(vertices[v]);
                    Vector3 originalLocalPos = localMatrix.MultiplyPoint3x4(mesh.vertices[v]);

                    // Считаем чистую дельту без влияния поворотов импорта
                    Vector3 deltaPosition = animatedLocalPos - originalLocalPos;

                    Color pixelColor = new Color(deltaPosition.x, deltaPosition.y, deltaPosition.z, 1.0f);
                    vatTexture.SetPixel(v, currentPixelY, pixelColor);
                }

                currentPixelY++;
            }
        }

        vatTexture.Apply();
        DestroyImmediate(tempGO);
        DestroyImmediate(bakedMesh);

        byte[] bytes = vatTexture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
        string savePath = EditorUtility.SaveFilePanelInProject("Сохранить VAT текстуру", targetPrefab.name + "_VAT", "exr", "Выберите путь");
        
        if (!string.IsNullOrEmpty(savePath))
        {
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.Refresh();
            
            Debug.Log(logReport);
            EditorUtility.DisplayDialog("Готово!", "Текстура успешно запечена с исправлением координат!", "ОК");
        }
    }

}
#endif