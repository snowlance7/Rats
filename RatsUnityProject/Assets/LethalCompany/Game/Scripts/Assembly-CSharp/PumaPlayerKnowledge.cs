using System;
using GameNetcodeStuff;

[Serializable]
public class PumaPlayerKnowledge
{
	public PlayerControllerB playerScript;

	public float timeSinceSeeingPuma;

	public float timeSincePumaSeeing;

	public float seeAngle;

	public bool seenByPuma;

	public bool seenByPumaThroughTrees;

	public PumaPlayerKnowledge(PlayerControllerB playerController)
	{
		playerScript = playerController;
	}
}
