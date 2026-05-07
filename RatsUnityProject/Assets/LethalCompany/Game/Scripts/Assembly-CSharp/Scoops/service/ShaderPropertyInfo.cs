using UnityEngine;
using UnityEngine.Rendering;

namespace Scoops.service
{
	public struct ShaderPropertyInfo
	{
		public string name;

		public ShaderPropertyType type;

		public Color _color;

		public float _float;

		public int _int;

		public Texture _texture;

		public Vector4 _vector;
	}
}
