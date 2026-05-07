using DunGen;
using DunGen.Graph;
using DunGen.Tags;
using UnityEngine;

public class MineshaftTunnelConnectionRule : MonoBehaviour
{
	public DungeonFlow mineshaftFlow;

	private int Priority = 10000;

	private TileConnectionRule rule;

	private TileConnectionRule rule2;

	public Tag caveTag;

	public Tag tunnelTag;

	private TileProxy caveTile;

	public void OnEnable()
	{
		DungeonGenerator.OnAnyDungeonGenerationStatusChanged += AddMineshaftRule;
	}

	private void AddMineshaftRule(DungeonGenerator generator, GenerationStatus status)
	{
		if (generator.DungeonFlow == mineshaftFlow)
		{
			if (status == GenerationStatus.Branching)
			{
				rule = new TileConnectionRule(DisallowMixingCavesWithTunnels, Priority);
				DoorwayPairFinder.CustomConnectionRules.Add(rule);
			}
			switch (status)
			{
			case GenerationStatus.InstantiatingTiles:
				rule2 = new TileConnectionRule(AllowCaveTunnelConnections, Priority - 1);
				DoorwayPairFinder.CustomConnectionRules.Add(rule2);
				break;
			case GenerationStatus.NotStarted:
			case GenerationStatus.Complete:
			case GenerationStatus.Failed:
				ClearRules();
				break;
			}
		}
		else
		{
			ClearRules();
		}
	}

	private void ClearRules()
	{
		if (rule != null)
		{
			DoorwayPairFinder.CustomConnectionRules.Remove(rule);
			rule = null;
		}
		if (rule2 != null)
		{
			DoorwayPairFinder.CustomConnectionRules.Remove(rule2);
			rule2 = null;
		}
		caveTile = null;
	}

	public void OnDisable()
	{
		ClearRules();
		DungeonGenerator.OnAnyDungeonGenerationStatusChanged -= AddMineshaftRule;
	}

	private TileConnectionRule.ConnectionResult DisallowMixingCavesWithTunnels(ProposedConnection connection)
	{
		if (connection.PreviousTile.Tags.Tags.Contains(caveTag))
		{
			caveTile = connection.PreviousTile;
			if (!connection.NextTile.Tags.Tags.Contains(tunnelTag))
			{
				return TileConnectionRule.ConnectionResult.Passthrough;
			}
		}
		else
		{
			if (!connection.NextTile.Tags.Tags.Contains(caveTag))
			{
				return TileConnectionRule.ConnectionResult.Passthrough;
			}
			caveTile = connection.NextTile;
			if (!connection.PreviousTile.Tags.Tags.Contains(tunnelTag))
			{
				return TileConnectionRule.ConnectionResult.Passthrough;
			}
		}
		foreach (DoorwayProxy usedDoorway in caveTile.UsedDoorways)
		{
			if (usedDoorway.ConnectedDoorway == null)
			{
				continue;
			}
			foreach (Tag tag in usedDoorway.ConnectedDoorway.TileProxy.Tags)
			{
				if (tag == tunnelTag)
				{
					return TileConnectionRule.ConnectionResult.Deny;
				}
			}
			foreach (DoorwayProxy usedDoorway2 in usedDoorway.ConnectedDoorway.TileProxy.UsedDoorways)
			{
				if (usedDoorway2.ConnectedDoorway == null)
				{
					continue;
				}
				foreach (Tag tag2 in usedDoorway2.ConnectedDoorway.TileProxy.Tags)
				{
					if (tag2 == tunnelTag)
					{
						return TileConnectionRule.ConnectionResult.Deny;
					}
				}
			}
		}
		return TileConnectionRule.ConnectionResult.Passthrough;
	}

	private TileConnectionRule.ConnectionResult AllowCaveTunnelConnections(ProposedConnection connection)
	{
		if (connection.PreviousDoorway.Socket != connection.NextDoorway.Socket)
		{
			return TileConnectionRule.ConnectionResult.Deny;
		}
		return TileConnectionRule.ConnectionResult.Allow;
	}
}
