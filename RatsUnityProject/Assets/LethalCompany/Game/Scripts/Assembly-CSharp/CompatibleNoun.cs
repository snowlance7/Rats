using System;

[Serializable]
public class CompatibleNoun
{
	public TerminalKeyword noun;

	public TerminalNode result;

	public CompatibleNoun(TerminalKeyword newNoun, TerminalNode newResult)
	{
		noun = newNoun;
		result = newResult;
	}
}
