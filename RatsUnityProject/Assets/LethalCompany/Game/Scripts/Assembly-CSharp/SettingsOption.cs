using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsOption : MonoBehaviour
{
	public SettingsOptionType optionType;

	public TextMeshProUGUI textElement;

	public Image toggleImage;

	public Sprite enabledImage;

	public Sprite disabledImage;

	[Header("Key rebinding")]
	public InputActionReference rebindableAction;

	public int rebindableActionBindingIndex = -1;

	public bool gamepadOnlyRebinding;

	public bool requireButtonType;

	public GameObject waitingForInput;

	public TextMeshProUGUI currentlyUsedKeyText;

	public void FadeBackgroundOfSettings()
	{
		IngamePlayerSettings.Instance.SetQuickMenuBGTransparent();
	}

	public void CancelRebinds()
	{
		IngamePlayerSettings.Instance.CancelRebind();
	}

	public void SetBindingToCurrentSetting()
	{
		if (optionType == SettingsOptionType.ChangeBinding)
		{
			int bindingIndexForControl = rebindableAction.action.GetBindingIndexForControl(rebindableAction.action.controls[0]);
			currentlyUsedKeyText.text = InputControlPath.ToHumanReadableString(rebindableAction.action.bindings[bindingIndexForControl].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
		}
	}

	public void ResetBindingsToDefaultButton()
	{
		IngamePlayerSettings.Instance.ResetAllKeybinds();
	}

	public void SetValueToMatchSettings()
	{
		switch (optionType)
		{
		case SettingsOptionType.ChangeBinding:
			SetBindingToCurrentSetting();
			break;
		case SettingsOptionType.LookSens:
			base.gameObject.GetComponentInChildren<Slider>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.lookSensitivity);
			break;
		case SettingsOptionType.Gamma:
			base.gameObject.GetComponentInChildren<Slider>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.gammaSetting / 0.05f);
			break;
		case SettingsOptionType.MicEnabled:
			ToggleEnabledImage(4);
			break;
		case SettingsOptionType.MasterVolume:
			base.gameObject.GetComponentInChildren<Slider>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.masterVolume * 100f);
			break;
		case SettingsOptionType.FramerateCap:
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.framerateCapIndex);
			break;
		case SettingsOptionType.FullscreenType:
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify((int)IngamePlayerSettings.Instance.settings.fullScreenType);
			break;
		case SettingsOptionType.InvertYAxis:
			ToggleEnabledImage(11);
			break;
		case SettingsOptionType.AdvancedLighting:
			ToggleEnabledImage(14);
			break;
		case SettingsOptionType.SpiderSafeMode:
			ToggleEnabledImage(12);
			break;
		case SettingsOptionType.ToggleSprint:
			ToggleEnabledImage(17);
			break;
		case SettingsOptionType.HeadBobbing:
			ToggleEnabledImage(18);
			break;
		case SettingsOptionType.TerrainGrass:
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.terrainGrassDistance);
			break;
		case SettingsOptionType.MotionBlur:
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.motionBlur);
			break;
		case SettingsOptionType.PixelRes:
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.pixelRes);
			break;
		case SettingsOptionType.OnlineMode:
		case SettingsOptionType.MicPushToTalk:
		case SettingsOptionType.MicDevice:
		case SettingsOptionType.CancelOrConfirm:
			break;
		}
	}

	public void SetMasterVolume()
	{
		AudioListener.volume = IngamePlayerSettings.Instance.settings.masterVolume;
	}

	public void StartRebindKey()
	{
		IngamePlayerSettings.Instance.RebindKey(rebindableAction, this, rebindableActionBindingIndex, gamepadOnlyRebinding);
	}

	public void OnEnable()
	{
		if (optionType == SettingsOptionType.MicDevice)
		{
			IngamePlayerSettings.Instance.RefreshAndDisplayCurrentMicrophone();
		}
		if (optionType == SettingsOptionType.TerrainGrass)
		{
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.terrainGrassDistance);
		}
		if (optionType == SettingsOptionType.MotionBlur)
		{
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.motionBlur);
		}
		if (optionType == SettingsOptionType.PixelRes)
		{
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify(IngamePlayerSettings.Instance.settings.pixelRes);
		}
		if (optionType == SettingsOptionType.FullscreenType)
		{
			base.gameObject.GetComponentInChildren<TMP_Dropdown>().SetValueWithoutNotify((int)IngamePlayerSettings.Instance.settings.fullScreenType);
		}
	}

	public void OnDisable()
	{
		if (optionType == SettingsOptionType.ChangeBinding)
		{
			if (IngamePlayerSettings.Instance.rebindingOperation != null)
			{
				IngamePlayerSettings.Instance.CancelRebind();
			}
			currentlyUsedKeyText.enabled = true;
			waitingForInput.SetActive(value: false);
		}
	}

	public void SetSettingsOptionInt(int value)
	{
		IngamePlayerSettings.Instance.SetOption(optionType, value);
	}

	public void SetSettingsOptionFloat(float value)
	{
		IngamePlayerSettings.Instance.SetOption(optionType, (int)value);
	}

	public void ToggleEnabledImage(int optionType)
	{
		if (toggleImage == null)
		{
			return;
		}
		bool flag = false;
		switch (optionType)
		{
		case 4:
			flag = IngamePlayerSettings.Instance.unsavedSettings.micEnabled;
			break;
		case 11:
			flag = IngamePlayerSettings.Instance.unsavedSettings.invertYAxis;
			break;
		case 14:
			flag = IngamePlayerSettings.Instance.unsavedSettings.advancedLightMode;
			break;
		case 12:
			flag = IngamePlayerSettings.Instance.unsavedSettings.spiderSafeMode;
			break;
		case 17:
			flag = IngamePlayerSettings.Instance.unsavedSettings.toggleSprint;
			break;
		case 18:
			flag = IngamePlayerSettings.Instance.unsavedSettings.headBobbing;
			break;
		case 19:
			flag = IngamePlayerSettings.Instance.flipCamera;
			break;
		}
		if (flag)
		{
			toggleImage.sprite = enabledImage;
			if (textElement != null)
			{
				textElement.text = "ENABLED";
			}
		}
		else
		{
			toggleImage.sprite = disabledImage;
			if (textElement != null)
			{
				textElement.text = "DISABLED";
			}
		}
	}

	public void ConfirmSettings()
	{
		IngamePlayerSettings.Instance.SaveChangedSettings();
	}

	public void ResetSettingsToDefault()
	{
		IngamePlayerSettings.Instance.ResetSettingsToDefault();
	}

	public void CancelSettings()
	{
		IngamePlayerSettings.Instance.DiscardChangedSettings();
	}
}
