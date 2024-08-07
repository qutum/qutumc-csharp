//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

#pragma warning disable IDE0059
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Linq;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Ser = (SyntStr t, SynterStr s);

static class TestExtension
{
	public static Ser Eq(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		if (from != null) AreEqual(from, s.t.from);
		if (to != null) AreEqual(to, s.t.to);
		if (v != null) AreEqual(v,
			v is string && s.t.err == 0 ? s.s.ler.Lexs(s.t.from, s.t.to) : s.t.info);
		return s;
	}

	public static Ser h(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, from, to, v, err);
	public static Ser t(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, from, to, v, err);
	public static Ser n(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, from, to, v, err);
	public static Ser p(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, from, to, v, err);

	public static Ser H(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> h(s, name, from, to, v, err).Leaf();
	public static Ser T(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> t(s, name, from, to, v, err).Leaf();
	public static Ser N(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> n(s, name, from, to, v, err).Leaf();
	public static Ser P(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> p(s, name, from, to, v, err).Leaf();

	public static Ser Leaf(this Ser s) { AreEqual(null, s.t.head); return s; }
	public static Ser u(this Ser s) { AreEqual(null, s.t.next); AreNotEqual(null, s.t.up); return (s.t.up, s.s); }
	public static Ser U(this Ser s) { AreEqual(null, s.t.next); AreEqual(null, s.t.up); return (s.t.up, s.s); }
	public static Ser uu(this Ser s) => u(u(s));
	public static Ser uU(this Ser s) => U(u(s));
	public static Ser uuu(this Ser s) => u(u(u(s)));
	public static Ser uuU(this Ser s) => U(u(u(s)));
	public static Ser uuuu(this Ser s) => u(u(u(u(s))));
	public static Ser uuuU(this Ser s) => U(u(u(u(s))));
	public static Ser uuuuU(this Ser s) => U(u(u(u(u(s)))));
	public static Ser uuuuuU(this Ser s) => U(u(u(u(u(u(s))))));
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	SynterStr ser;

	public void NewSer(string Alts, char[] keys, ushort[] names, SynForm[] forms)
	{
		var alts = Alts.Split('\n', ' ').Select(alt => new SynAlt<string> {
			name = alt[0..1],
			len = alt.Length - 2,
			lex = alt.IndexOfAny(keys, 2) - 2,
			synt = alt[1] switch { '+' => 1, '-' => -1, _ => 0 },
		}).ToArray();
		var ks = keys.Select(k => (ushort)k).Prepend(default); // { other... }
		foreach (var form in forms)
			if (form != null) {
				form.modes = form.modes.Prepend(default).ToArray();
				Array.Sort(form.keys = ks.ToArray(), form.modes, 1, keys.Length);
				if (form.pushs != null)
					Array.Sort(form.names = names[0..], form.pushs);
			}
		ser = new(ler => ler.Lex(), name => name[0], alts, forms) { dump = 3 };
	}

	public void True(string read) => IsTrue(ser.Begin(new LerStr(read)).Check());
	public void False(string read) => IsFalse(ser.Begin(new LerStr(read)).Check());

	public Ser Parse(string read) => (ser.Begin(new LerStr(read)).Parse().Dump(), ser);


	[TestMethod]
	public void TigerF325()
	{
		NewSer("S=E E=T+E E=T T=a", ['a', 'b', '+', '\0'], ['E', 'T'], [ null,
			new() { modes = [5, 5,   0,   0], pushs = [2, 3] },
			new() { modes = [0, 0,   0,-2-0],                },
			new() { modes = [0, 0,   4,-2-2],                },
			new() { modes = [5, 5,   0,   0], pushs = [6, 3] },
			new() { modes = [0, 0,-2-3,-2-3],                },
			new() { modes = [0, 0,   0,-2-1],                },
		]);
		True("a"); True("b");
		True("a+b"); True("a+b+a");
		False(""); False("c"); False("+");
		False("ab"); False("a+"); False("+b");
		False("a+b+"); False("+a+b"); False("a++b");
		var t = Parse("a+b+a");
		t = t/**/.Eq("S", 0, 5).h("E");
		t = t/**/				.H("T", 0, 1, v: 'a');
		t = t/**/				.n("E", 2, 5, v: '+').H("T", 2, 3, v: 'b');
		t = t/**/									.n("E").H("T", 4, 5, v: 'a').uuuuU();
	}

	[TestMethod]
	public void TigerF328()
	{
		NewSer("Z=S S=V=E S-E E-V V=a V=*E", ['a', 'b', '*', '=', '\0'], ['S', 'E', 'V'], [ null,
			new() { modes = [8, 8, 6,   0,   0], pushs = [2,  5, 3] },
			new() { modes = [0, 0, 0,   0,-2-0],                    },
			new() { modes = [0, 0, 0,   4,-2-3],                    },
			new() { modes = [8, 8, 6,   0,   0], pushs = [0,  9, 7] },
			new() { modes = [0, 0, 0,   0,-2-2],                    },
			new() { modes = [8, 8, 6,   0,   0], pushs = [0, 10, 7] },
			new() { modes = [0, 0, 0,-2-3,-2-3],                    },
			new() { modes = [0, 0, 0,-2-4,-2-4],                    },
			new() { modes = [0, 0, 0,   0,-2-1],                    },
			new() { modes = [0, 0, 0,-2-5,-2-5],                    },
		]);
		True("a"); True("*b"); True("**a");
		True("a=b"); True("a=*b"); True("b=**a");
		True("*a=b"); True("*b=*a"); True("*a=**b");
		True("**a=b"); True("**a=*b"); True("**b=**a");
		var t = Parse("**a");
		t = t/**/.Eq("Z", 0, 3).h("V", v: '*').h("V", v: '*').H("V", v: 'a').uuuU();
		t = Parse("**b=***a").Eq("Z", 0, 8);
		t = t/**/.h("S", v: '=').h("V", v: '*').h("V", v: '*').H("V", v: 'b').uu();
		t = t/**/				.n("V", v: '*').h("V", v: '*').h("V", v: '*').H("V", v: 'a').uuuuuU();
	}

	[TestMethod]
	public void TigerF322()
	{
		NewSer("Z=S S=(L) S=a L-S L=L,S", ['(', ')', 'a', 'b', ',', '\0'], ['S', 'L'], [ null,
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [4, 0] },
			new() { modes = [-2-2,-2-2,-2-2,-2-2,-2-2,-2-2],                },
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [7, 5] },
			new() { modes = [   0,   0,   0,   0,   0,-2-0],                },
			new() { modes = [   0,   6,   0,   0,   8,   0],                },
			new() { modes = [-2-1,-2-1,-2-1,-2-1,-2-1,-2-1],                },
			new() { modes = [-2-3,-2-3,-2-3,-2-3,-2-3,-2-3],                },
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [9, 0] },
			new() { modes = [-2-4,-2-4,-2-4,-2-4,-2-4,-2-4],                },
		]);
		True("a"); True("b");
		True("(a)"); True("((a))");
		True("(a,b)"); True("(a,((b)))"); True("(((a)),(b))");
		True("((a,b),a)"); True("(a,((a,b)))"); True("((a,b),(b,a))");
		True("(a,b,a)"); True("((a),((b)),a)"); True("(a,b,(a))");
		True("((a,b),(b,a),(a,a))"); True("((a,b),((a,(b,a)),a),(a,a))");
		False("z"); False(","); False("("); False(")");
		False("ab"); False("a,b"); False("a,,b");
		False("(a"); False("b)"); False("a,"); False(",b");
		False("(a,b))"); False("((a)"); False("(b,)");
		False("((a,b)"); False("((a,(b,a)),b");
		var t = Parse("((a,b),((a,(b,a)),b),(a,b))").Eq("Z", 0, 27);
		t = t/**/.h("S", v: '(').h("L", v: ',');
		t = t/**/	.h("L", v: ',').h("S", 1, 6, '(');
		t = t/**/						.h("L", v: ',').H("S", v: 'a').N("S", v: 'b').uu();
		t = t/**/			.n("S", 7, 20, '(');
		t = t/**/				.h("L", v: ',').h("S", 8, 17, '(');
		t = t/**/									.h("L", v: ',').H("S", v: 'a');
		t = t/**/										.n("S", 11, 16, '(').h("L", v: ',');
		t = t/**/											.H("S", v: 'b').N("S", v: 'a').uuuu();
		t = t/**/								.N("S", v: 'b').uuu();
		t = t/**/	.n("S", 21, 26, '(').h("L", v: ',');
		t = t/**/						.H("S", v: 'a').N("S", v: 'b').uuuuuU();
	}

	[TestMethod]
	public void Dragon2F451()
	{
		NewSer("Z=S S=iSeS S=iS S=a", ['i', 'e', 'a', 'b', 'c', '\0'], ['S'], [
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [1] },
			new() { modes = [-1,  -1, -1, -1, -1,-2-0],             },
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [4] },
			new() { modes = [-1,-2-3, -1, -1, -1,-2-3],             },
			new() { modes = [-1,   5, -1, -1, -1,-2-2],             },
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [6] },
			new() { modes = [-1,-2-1, -1, -1, -1, -2-1],             },
		]);
		True("b"); True("ia"); True("iaeb");
		True("iia"); True("iiaeb"); True("iaeib");
		True("iiaebec"); True("iaeibec");
		True("iiia"); True("iiiaeb"); True("iiaeib"); True("iaeiib");
		True("iiiaebec"); True("iiaeibec"); True("iiaebeic");
		True("iaeiibec"); True("iaeibeic");
		True("iiiaebecea"); True("iiaeibecea"); True("iaeiibecea"); True("iaeibeiaec");
		var t = Parse("iiiaebec");
		t = t/**/.h(v: 'i').h(v: 'i').h(v: 'i');
		t = t/**/						.H(v: 'a').N(v: 'b').u();
		t = t/**/					.N(v: 'c').uuuU();
		t = Parse("iiaebeic");
		t = t/**/.h(v: 'i').h(v: 'i');
		t = t/**/				.H(v: 'a').N(v: 'b').u();
		t = t/**/			.n(v: 'i').H(v: 'c').uuuU();
		t = Parse("iiaeibecea");
		t = t/**/.h(v: 'i').h(v: 'i');
		t = t/**/				.H(v: 'a').n(v: 'i');
		t = t/**/					.H(v: 'b').N(v: 'c').uu();
		t = t/**/			.N(v: 'a').uuU();
		t = Parse("iaeiibecea");
		t = t/**/.h(v: 'i').H(v: 'a');
		t = t/**/			.n(v: 'i').h(v: 'i');
		t = t/**/						.H(v: 'b').N(v: 'c').u();
		t = t/**/					.N(v: 'a').uuuU();
	}

	[TestMethod]
	public void Dragon2F449()
	{
		NewSer("E=E+E E=E*E E-(E) E=a", ['a', 'b', '+', '*', '(', ')', '\0'], ['E'], [
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [1] },
			new() { modes = [-1, -1,   4,   5, -1,  -1,  -1],             },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [6] },
			new() { modes = [-1, -1,-2-3,-2-3, -1,-2-3,-2-3],             },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [7] },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [8] },
			new() { modes = [-1, -1,   4,   5, -1,   9,  -1],             },
			new() { modes = [-1, -1,-2-0,   5, -1,-2-0,-2-0],             },
			new() { modes = [-1, -1,-2-1,-2-1, -1,-2-1,-2-1],             },
			new() { modes = [-1, -1,-2-2,-2-2, -1,-2-2,-2-2],             },
		]); // no E'->E
		True("a"); True("a+b"); True("a*b");
		True("(b)"); True("(b*a)"); True("(b+a)"); True("(a)*b"); True("a+(b)");
		True("a+b+a"); True("a*b*a"); True("a+b*a"); True("b*a+b");
		True("(a)+a+a"); True("b*(b)*b"); True("a+(b*a+b)*b");
		True("(a)+(a*a)"); True("(b*(b))*b"); True("a+((b*a+b)*b)");
		False("+"); False("a+"); False("*b"); False("a++b");
		False("(b)a"); False("a*((b"); False("b+a)"); False("(a*)+b");
		var t = Parse("a+b*((b*a+b)*b)");
		t = t/**/.Eq(v: '+').H(v: 'a');
		t = t/**/		.n("E", 2, 15, '*').H(v: 'b');
		t = t/**/						.n("E", 5, 14, '*').h("E", 6, 11, '+');
		t = t/**/												.h(v: '*').H(v: 'b').N(v: 'a').u();
		t = t/**/												.N(v: 'b').u();
		t = t/**/											.N(v: 'b').uuuU();
	}
}
