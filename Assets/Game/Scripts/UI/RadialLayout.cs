using UnityEngine;
using UnityEngine.UI;

public class RadialLayout : LayoutGroup
{
    [Range(0f, 100f)]
    public float radius = 100f;
    [Range(0f, 360f)] public float minAngle = 0f;
    [Range(0f, 360f)] public float maxAngle = 360f;
    [Range(0f, 360f)] public float startAngle = 0f;

    protected override void OnEnable() { base.OnEnable(); CalculateRadial(); }
    public override void SetLayoutHorizontal() => CalculateRadial();
    public override void SetLayoutVertical() => CalculateRadial();
    public override void CalculateLayoutInputVertical() => CalculateRadial();

    private void CalculateRadial()
    {
        m_Tracker.Clear();
        if (transform.childCount == 0) return;

        float step = (maxAngle - minAngle) / (transform.childCount > 1 ? transform.childCount - 1 : 1);
        float currentAngle = startAngle;

        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform child = (RectTransform)transform.GetChild(i);
            if (child == null || !child.gameObject.activeSelf) continue;

            m_Tracker.Add(this, child, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Pivot);

            float radians = currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Cos(radians) * radius;
            float y = Mathf.Sin(radians) * radius;

            child.anchoredPosition = new Vector2(x, y);
            
            child.anchorMin = child.anchorMax = child.pivot = new Vector2(0.5f, 0.5f);
            
            currentAngle += step;
        }
    }
}
