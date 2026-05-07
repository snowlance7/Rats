using UnityEngine;
using UnityEngine.Scripting;

namespace ES3Types
{
	[Preserve]
	[ES3Properties(new string[] { "time", "count", "minCount", "maxCount", "cycleCount", "repeatInterval", "probability" })]
	public class ES3Type_Burst : ES3Type
	{
		public static ES3Type Instance;

		public ES3Type_Burst()
			: base(typeof(ParticleSystem.Burst))
		{
			Instance = this;
			priority = 1;
		}

		public override void Write(object obj, ES3Writer writer)
		{
			ParticleSystem.Burst burst = (ParticleSystem.Burst)obj;
			writer.WriteProperty("time", burst.time, ES3Type_float.Instance);
			writer.WriteProperty("count", burst.count, ES3Type_MinMaxCurve.Instance);
			writer.WriteProperty("minCount", burst.minCount, ES3Type_short.Instance);
			writer.WriteProperty("maxCount", burst.maxCount, ES3Type_short.Instance);
			writer.WriteProperty("cycleCount", burst.cycleCount, ES3Type_int.Instance);
			writer.WriteProperty("repeatInterval", burst.repeatInterval, ES3Type_float.Instance);
			writer.WriteProperty("probability", burst.probability, ES3Type_float.Instance);
		}

		public override object Read<T>(ES3Reader reader)
		{
			ParticleSystem.Burst burst = default(ParticleSystem.Burst);
			string text;
			while ((text = reader.ReadPropertyName()) != null)
			{
				switch (text)
				{
				case "time":
					burst.time = reader.Read<float>(ES3Type_float.Instance);
					break;
				case "count":
					burst.count = reader.Read<ParticleSystem.MinMaxCurve>(ES3Type_MinMaxCurve.Instance);
					break;
				case "minCount":
					burst.minCount = reader.Read<short>(ES3Type_short.Instance);
					break;
				case "maxCount":
					burst.maxCount = reader.Read<short>(ES3Type_short.Instance);
					break;
				case "cycleCount":
					burst.cycleCount = reader.Read<int>(ES3Type_int.Instance);
					break;
				case "repeatInterval":
					burst.repeatInterval = reader.Read<float>(ES3Type_float.Instance);
					break;
				case "probability":
					burst.probability = reader.Read<float>(ES3Type_float.Instance);
					break;
				default:
					reader.Skip();
					break;
				}
			}
			return burst;
		}
	}
}
