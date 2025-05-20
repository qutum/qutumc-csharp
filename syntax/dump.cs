//
// Qutum 10 Compiler
// Copyright 2008-2025 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;

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

public partial class Lexier
{
	public string Dumper(bool all = true, bool hex = false)
	{
		if (dump)
			using (var env = EnvWriter.Use())
				env.WriteLine(G.Dumper());
		return string.Join(" ",
			(all ? errs.Concat(lexs.Seg((1, loc))).Concat(blanks ?? []).OrderBy(l => l.j.on)
				: (IEnumerable<Lexi<L>>)lexs.Seg((1, loc)))
			.Select(t => t.Dumper(hex ? DumperHex : Dumper)).ToArray());
	}

	public static string Dumper(object d)
		=> d is object[] s ? string.Join(',', s) + "," : d?.ToString() ?? "";

	public static string DumperHex(object d)
	{
		if (d is int i) return i.ToString("x8");
		if (d is not float f) return Dumper(d);
		var b = BitConverter.SingleToInt32Bits(f);
		if (b == 0) return "0p0";
		if (b == 1 << 31) return "-0p0";
		var sub = (b & 0x7f800000) == 0;
		return new StrMake(out var _) + (b < 0 ? "-" : "") + (sub ? "0." : "1.") +
			(b << 1 & 0xFFffff).ToString("x6") + 'p' + (sbyte)(sub ? -126 : (byte)(b >> 23) - 127);
	}
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
