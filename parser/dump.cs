//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System.Diagnostics;
using System.Linq;

[assembly: DebuggerTypeProxy(typeof(Dumpers.Form), Target = typeof(SynForm))]
[assembly: DebuggerTypeProxy(typeof(Dumpers.Stack), Target = typeof((SynForm, int, object)))]

namespace qutum.parser;

public static class Dumpers
{
	[DebuggerDisplay("{d,nq}")]
	public struct Str
	{
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public string d;
		public override readonly string ToString() => d;
		public static implicit operator string(Str d) => d.d;
		public static explicit operator Str(string d) => new() { d = d };

	}

	public struct Form
	{
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public string dump;
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public Str[] s;
		public Str err;

		public Form(SynForm f)
		{
			if (f.dump is Form d)
				(dump, s) = (d.dump, d.s);
			else // DebuggerDisplay stupidly ignored for DebuggerTypeProxy
				(_, _, _) = (s = f.ToString().Split('\n').Select(s => (Str)s).ToArray(),
					dump = $"form {f.index} size {s?.Length}",
					f.dump = this);
			err = (Str)f.err;
		}
		public override readonly string ToString() => dump;
	}

	[DebuggerDisplay("title")] // no effect
	public struct Stack((SynForm form, int loc, object synt) d)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public SynForm zform = d.form;
		public int loc = d.loc;
		public object synt = d.synt;
		public override readonly string ToString() => $"@{loc} {zform}"; // no effect
	}
}
