using UnityEngine;

public class ConditionalOnTwoObjects : MonoBehaviour
{
	public GameObject conditionA;

	public GameObject conditionB;

	public bool invert;

	private void OnEnable()
	{
		if (RoundManager.Instance.dungeonCompletedGenerating)
		{
			RunCondition();
		}
		else
		{
			RoundManager.Instance.OnFinishedGeneratingDungeon += RunCondition;
		}
	}

	private void OnDisable()
	{
		RoundManager.Instance.OnFinishedGeneratingDungeon -= RunCondition;
	}

	private void RunCondition()
	{
		if (invert)
		{
			if (conditionA == null || conditionB == null)
			{
				Object.Destroy(this);
			}
			else
			{
				Object.Destroy(base.gameObject);
			}
		}
		else if (conditionA == null && conditionB == null)
		{
			Object.Destroy(base.gameObject);
		}
		else
		{
			Object.Destroy(this);
		}
	}
}
