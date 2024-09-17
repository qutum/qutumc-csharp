//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;

namespace qutum.syntax;

using Kord = char;
using L = Lex;
using Nord = ushort;

public class Dumps
{
	public static L Lex(Kord o)
	{
		if (ordins == null) {
			var s = new L[256];
			foreach (var k in Enum.GetValues<L>())
				if (k.IsKind() || k.IsSingle())
					s[k.Ordin()] = k;
			s[default] = default;
			ordins = s;
		}
		return ordins[o];
	}
	private static L[] ordins;
}

public partial class Synter
{
	protected override void InitDump()
	{
		base.InitDump();
		if (mer != null)
			dumper = mer.Dumper;
	}

	public override string Dumper(object d)
	{
		if (d is Kord k && k != default) return Dumps.Lex(k).ToString();
		if (d is Nord n) return ((Syn)n).ToString();
		return base.Dumper(d);
	}
}
