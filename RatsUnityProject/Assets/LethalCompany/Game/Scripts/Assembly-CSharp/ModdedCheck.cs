using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ModdedCheck
{
	private static ModdedState moddedState;

	public static string GetCommandLineArg(string key)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length - 1; i++)
		{
			if (commandLineArgs[i].Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return commandLineArgs[i + 1];
			}
		}
		return null;
	}

	public static ModdedState GetModdedState()
	{
		try
		{
			if (moddedState != ModdedState.Unknown)
			{
				return moddedState;
			}
			if (File.Exists("winhttp.dll"))
			{
				bool flag = false;
				string path = "";
				if (File.Exists("doorstop_config.ini"))
				{
					try
					{
						HashSet<string> hashSet = new HashSet<string>();
						string[] array = File.ReadAllLines("doorstop_config.ini");
						foreach (string text in array)
						{
							if (string.IsNullOrWhiteSpace(text) || text.StartsWith("#"))
							{
								continue;
							}
							string[] array2 = text.Split('=');
							if (array2.Length != 2)
							{
								continue;
							}
							string text2 = array2[0].Trim();
							string text3 = array2[1].Trim();
							bool result;
							if (!(text2 == "enabled"))
							{
								if (text2 == "target_assembly")
								{
									path = text3;
									hashSet.Add(text2);
								}
							}
							else if (bool.TryParse(text3, out result))
							{
								flag = result;
								hashSet.Add(text2);
							}
							if (hashSet.Count >= 2)
							{
								break;
							}
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
					}
				}
				if (bool.TryParse(GetCommandLineArg("--doorstop-enabled"), out var result2))
				{
					flag = result2;
				}
				string commandLineArg = GetCommandLineArg("--doorstop-target-assembly");
				if (!string.IsNullOrEmpty(commandLineArg))
				{
					path = commandLineArg;
				}
				if (flag && File.Exists(path))
				{
					moddedState = ModdedState.Modded;
					return moddedState;
				}
			}
			moddedState = ModdedState.Vanilla;
			return moddedState;
		}
		catch (Exception arg)
		{
			Debug.Log($"Unable to check if client is modded: {arg}");
			return ModdedState.Unknown;
		}
	}
}
