using UnityEngine;
using UnityEngine.EventSystems;

public abstract class DragableUIWindow : UIScreen,IDragableWindow
{
    [Header("Drag Settings")]
    [SerializeField] protected RectTransform dragHandle; 
    [SerializeField] protected RectTransform windowToMove; 
    
    private Vector2 dragOffset;
    private Canvas canvas;
    private RectTransform canvasRect;
    private bool isValidDrag = false;
    
    public override void Initialize()
    {
        if (windowToMove == null) windowToMove = transform.parent as RectTransform;
        
        if (dragHandle == null) dragHandle = GetComponent<RectTransform>();
        
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
    }
    
    private bool IsPointerOverDragHandle(PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(dragHandle, eventData.position, eventData.pressEventCamera);
    }
    
    void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
    {
        if (!IsPointerOverDragHandle(eventData)) return;
        
        isValidDrag = true;
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowToMove.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            dragOffset = windowToMove.anchoredPosition - localPoint;
        }
        
        windowToMove.SetAsLastSibling(); 
    }
    
    void IDragHandler.OnDrag(PointerEventData eventData)
    {
        if (!isValidDrag) return;
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowToMove.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            windowToMove.anchoredPosition = localPoint + dragOffset;
            
            ClampToCanvasBounds();
        }
    }
    
    void IEndDragHandler.OnEndDrag(PointerEventData eventData)
    {
        isValidDrag = false;
    }
    void ClampToCanvasBounds()
    {
        if (canvasRect == null || windowToMove == null) return;
        
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 windowSize = windowToMove.rect.size;
        Vector2 windowPos = windowToMove.anchoredPosition;
        
        float minX = -canvasSize.x / 2 + windowSize.x / 2;
        float maxX = canvasSize.x / 2 - windowSize.x / 2;
        float minY = -canvasSize.y / 2 + windowSize.y / 2;
        float maxY = canvasSize.y / 2 - windowSize.y / 2;
        
        windowPos.x = Mathf.Clamp(windowPos.x, minX, maxX);
        windowPos.y = Mathf.Clamp(windowPos.y, minY, maxY);
        
        windowToMove.anchoredPosition = windowPos;
    }
}