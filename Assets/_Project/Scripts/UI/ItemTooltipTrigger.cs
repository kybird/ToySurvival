using UnityEngine;
using UnityEngine.EventSystems;

public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string _content;

    public void Setup(string content)
    {
        _content = content;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipHUD.Instance != null)
        {
            TooltipHUD.Instance.Show(_content, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipHUD.Instance != null)
        {
            TooltipHUD.Instance.Hide();
        }
    }
}
