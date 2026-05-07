using UnityEngine;
using UnityEngine.EventSystems;

public class HoverOverImageText : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	public bool unknown;

	public void OnPointerEnter(PointerEventData eventData)
	{
		MenuManager menuManager = Object.FindObjectOfType<MenuManager>();
		if (menuManager != null)
		{
			menuManager.HoverTip.gameObject.SetActive(value: true);
			menuManager.HoverTip.gameObject.transform.position = eventData.pointerCurrentRaycast.worldPosition;
			if (unknown)
			{
				menuManager.HoverTipText.text = "The host may be using a modified version of Lethal Company; you might experience unintended behavior. (Modding requires caution and is not supported.)";
			}
			else
			{
				menuManager.HoverTipText.text = "The host is detected to be using a modified version of Lethal Company; you are likely to experience unintended behavior. (Modding requires caution and is not supported.)";
			}
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		MenuManager menuManager = Object.FindObjectOfType<MenuManager>();
		if (menuManager != null)
		{
			menuManager.HoverTip.gameObject.SetActive(value: false);
		}
	}
}
