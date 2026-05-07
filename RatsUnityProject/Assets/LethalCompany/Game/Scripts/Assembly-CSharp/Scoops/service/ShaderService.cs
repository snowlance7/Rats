using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scoops.service
{
	public static class ShaderService
	{
		public static Dictionary<ShaderInfo, Shader> ShaderDict = new Dictionary<ShaderInfo, Shader>();

		public static List<Shader> dupedShader = new List<Shader>();

		public static string[] deDupeBlacklist;

		public static void DedupeAllShaders()
		{
			Material[] array = Resources.FindObjectsOfTypeAll<Material>();
			Array.Sort(array, delegate(Material x, Material y)
			{
				int num2 = (x.shader ? x.shader.GetInstanceID() : 0);
				uint num3 = (uint)((num2 < 0) ? (Math.Abs(num2) + int.MaxValue) : num2);
				int num4 = (y.shader ? y.shader.GetInstanceID() : 0);
				uint value2 = (uint)((num4 < 0) ? (Math.Abs(num4) + int.MaxValue) : num4);
				return num3.CompareTo(value2);
			});
			Material[] array2 = array;
			foreach (Material material in array2)
			{
				if (!(material != null) || !(material.shader != null))
				{
					continue;
				}
				ShaderInfo shaderInfo = new ShaderInfo(material.shader, material);
				if (ShaderDict.TryGetValue(shaderInfo, out var value))
				{
					if (value.GetInstanceID() != material.shader.GetInstanceID())
					{
						dupedShader.Add(material.shader);
						material.shader = value;
					}
				}
				else
				{
					AddToShaderDict(shaderInfo, material.shader);
				}
			}
			dupedShader.Clear();
			ShaderDict.Clear();
		}

		public static void AddToShaderDict(ShaderInfo info, Shader shader)
		{
			if (!(info.name == "") && !deDupeBlacklist.Contains(info.name.ToLower()))
			{
				ShaderDict.Add(info, shader);
			}
		}
	}
}
