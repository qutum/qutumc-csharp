//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
namespace qutum.syntax;

using Kord = char;
using Nord = ushort;

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
		if (d is Kord k && k != default) return Lexier.Ordin(k).ToString();
		if (d is Nord n) return ((Syn)n).ToString();
		return base.Dumper(d);
	}
}
