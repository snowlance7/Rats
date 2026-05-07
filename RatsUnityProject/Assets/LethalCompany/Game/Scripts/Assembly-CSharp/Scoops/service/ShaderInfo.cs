using UnityEngine;
using UnityEngine.Rendering;

namespace Scoops.service
{
	public class ShaderInfo
	{
		public string name;

		public int passCount;

		public int maxLOD;

		public int subshaderCount;

		public int renderQueue;

		public int propertyCount;

		public string propertyNames;

		public bool alphaCutoff;

		public int surfaceType;

		public uint keywordCount;

		public string enabledKeywords;

		public int enabledPasses;

		public ShaderInfo(Shader shader, Material material)
		{
			name = shader.name;
			passCount = 0;
			maxLOD = shader.maximumLOD;
			subshaderCount = shader.subshaderCount;
			for (int i = 0; i < subshaderCount; i++)
			{
				passCount += shader.GetPassCountInSubshader(i);
			}
			renderQueue = shader.renderQueue;
			propertyCount = shader.GetPropertyCount();
			propertyNames = "";
			for (int j = 0; j < propertyCount; j++)
			{
				propertyNames += shader.GetPropertyName(j);
			}
			keywordCount = shader.keywordSpace.keywordCount;
			enabledPasses = 0;
			for (int k = 0; k < material.passCount; k++)
			{
				enabledPasses += (material.GetShaderPassEnabled(material.GetPassName(k)) ? 1 : 0);
			}
			LocalKeyword[] array = material.enabledKeywords;
			foreach (LocalKeyword localKeyword in array)
			{
				enabledKeywords += localKeyword.name;
			}
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ShaderInfo);
		}

		public bool Equals(ShaderInfo s)
		{
			if ((object)s == null)
			{
				return false;
			}
			if ((object)this == s)
			{
				return true;
			}
			if (GetType() != s.GetType())
			{
				return false;
			}
			if (name == s.name && passCount == s.passCount && subshaderCount == s.subshaderCount && renderQueue == s.renderQueue && propertyCount == s.propertyCount && propertyNames == s.propertyNames && keywordCount == s.keywordCount && maxLOD == s.maxLOD && enabledKeywords == s.enabledKeywords)
			{
				return enabledPasses == s.enabledPasses;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (name, passCount, subshaderCount, renderQueue, propertyCount, propertyNames, maxLOD, enabledKeywords, enabledPasses).GetHashCode();
		}

		public static bool operator ==(ShaderInfo lhs, ShaderInfo rhs)
		{
			if ((object)lhs == null)
			{
				if ((object)rhs == null)
				{
					return true;
				}
				return false;
			}
			return lhs.Equals(rhs);
		}

		public static bool operator !=(ShaderInfo lhs, ShaderInfo rhs)
		{
			return !(lhs == rhs);
		}
	}
}
