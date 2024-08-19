//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
#pragma warning disable IDE0059 // Unnecessary assignment
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Linq;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Ser = (SyntStr t, SynterStr s);

static class Extension
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0305:Simplify collection initialization")]
	public static Ser Eq(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		if (from != null) AreEqual(from, s.t.from);
		if (to != null) AreEqual(to, s.t.to);
		if (err < 0 && d is string eq && s.t.info is string actu) {
			var As = actu.Split(SerMaker<char, string>.ErrMore);
			var es = eq.Split("  ");
			if (As.Length != es.Length || es.Zip(As).Any(ea
				=> !ea.First.Split(' ').ToHashSet().IsSubsetOf(ea.Second.Split(' ').ToHashSet())))
				Fail($"Expected Err <{eq}> Actual <{actu.Replace("\n", "  ")}>");
		}
		else if (d != null) AreEqual(d,
			d is string && s.t.err == 0 ? s.s.ler.Lexs(s.t.from, s.t.to) : s.t.info);
		return s;
	}

	public static Ser h(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, from, to, d, err);
	public static Ser t(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, from, to, d, err);
	public static Ser n(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, from, to, d, err);
	public static Ser p(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, from, to, d, err);

	public static Ser H(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> h(s, name, from, to, d, err).Leaf();
	public static Ser T(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> t(s, name, from, to, d, err).Leaf();
	public static Ser N(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> n(s, name, from, to, d, err).Leaf();
	public static Ser P(this Ser s,
		string name = null, int? from = null, int? to = null, object d = null, int err = 0)
		=> p(s, name, from, to, d, err).Leaf();

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

	internal SynterStr ser;

	public void NewSer(string Alts, char[] keys, ushort[] names, SynForm[] forms)
	{
		var alts = Alts.Split('\n', ' ').Select(a => new SynAlt<string> {
			name = a[0..1],
			size = (short)(a.Length - 2),
			lex = (short)(a.IndexOfAny(keys, 2) - 2),
			synt = a[1] switch { '+' => 1, '-' => -1, _ => 0 },
		}).ToArray();
		ushort[] ks = [0, .. keys]; // { other... }
		foreach (var f in forms)
			if (f != null) {
				Array.Sort(f.keys = ks[..], f.modes = [-1, .. f.modes], 1, keys.Length);
				if (f.pushs != null)
					Array.Sort(f.names = [0, .. names], f.pushs = [-1, .. f.pushs]);
			}
		ser = new(name => name[0], alts, forms) { dump = 3 };
	}

	public static short R(int alt) => SynForm.Reduce(alt);

	public void True(string read) => IsTrue(ser.Begin(new LerStr(read)).Check());
	public void False(string read) => IsFalse(ser.Begin(new LerStr(read)).Check());

	public Ser Parse(string read) => (ser.Begin(new LerStr(read)).Parse().Dump(), ser);

	[TestMethod]
	public void TigerF325()
	{
		NewSer("S=E E=T+E E=T T=a", ['a', 'b', '+', '\0'], ['E', 'T'], [ null,
			new() { modes = [5, 5,   0,   0], pushs = [2, 3] },
			new() { modes = [0, 0,   0,R(0)],                },
			new() { modes = [0, 0,   4,R(2)],                },
			new() { modes = [5, 5,   0,   0], pushs = [6, 3] },
			new() { modes = [0, 0,R(3),R(3)],                },
			new() { modes = [0, 0,   0,R(1)],                },
		]);
		DoTigerF325();
	}
	internal void DoTigerF325()
	{
		True("a"); True("b");
		True("a+b"); True("a+b+a");
		False(""); False("c"); False("+");
		False("ab"); False("a+"); False("+b");
		False("a+b+"); False("+a+b"); False("a++b");
		var t = Parse("a+b+a");
		t = t/**/.Eq("S", 0, 5).h("E");
		t = t/**/				.H("T", 0, 1, d: 'a');
		t = t/**/				.n("E", 2, 5, d: '+').H("T", 2, 3, d: 'b');
		t = t/**/									.n("E").H("T", 4, 5, d: 'a').uuuuU();
	}

	[TestMethod]
	public void TigerT328()
	{
		NewSer("Z=S S=V=E S-E E-V V=a V=*E", ['a', 'b', '*', '=', '\0'], ['S', 'E', 'V'], [ null,
			new() { modes = [8, 8, 6,   0,   0], pushs = [2,  5, 3] },
			new() { modes = [0, 0, 0,   0,R(0)],                    },
			new() { modes = [0, 0, 0,   4,R(3)],                    },
			new() { modes = [8, 8, 6,   0,   0], pushs = [0,  9, 7] },
			new() { modes = [0, 0, 0,   0,R(2)],                    },
			new() { modes = [8, 8, 6,   0,   0], pushs = [0, 10, 7] },
			new() { modes = [0, 0, 0,R(3),R(3)],                    },
			new() { modes = [0, 0, 0,R(4),R(4)],                    },
			new() { modes = [0, 0, 0,   0,R(1)],                    },
			new() { modes = [0, 0, 0,R(5),R(5)],                    },
		]);
		DoTigerT328();
	}
	internal void DoTigerT328()
	{
		True("a"); True("*b"); True("**a");
		True("a=b"); True("a=*b"); True("b=**a");
		True("*a=b"); True("*b=*a"); True("*a=**b");
		True("**a=b"); True("**a=*b"); True("**b=**a");
		var t = Parse("**a");
		t = t/**/.Eq("Z", 0, 3).h("V", d: '*').h("V", d: '*').H("V", d: 'a').uuuU();
		t = Parse("**b=***a").Eq("Z", 0, 8);
		t = t/**/.h("S", d: '=').h("V", d: '*').h("V", d: '*').H("V", d: 'b').uu();
		t = t/**/				.n("V", d: '*').h("V", d: '*').h("V", d: '*').H("V", d: 'a').uuuuuU();
	}

	[TestMethod]
	public void TigerT322()
	{
		NewSer("Z=S S=(L) S=a L-S L=L,S", ['(', ')', 'a', 'b', ',', '\0'], ['S', 'L'], [ null,
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [4, 0] },
			new() { modes = [R(2),R(2),R(2),R(2),R(2),R(2)],                },
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [7, 5] },
			new() { modes = [   0,   0,   0,   0,   0,R(0)],                },
			new() { modes = [   0,   6,   0,   0,   8,   0],                },
			new() { modes = [R(1),R(1),R(1),R(1),R(1),R(1)],                },
			new() { modes = [R(3),R(3),R(3),R(3),R(3),R(3)],                },
			new() { modes = [   3,   0,   2,   2,   0,   0], pushs = [9, 0] },
			new() { modes = [R(4),R(4),R(4),R(4),R(4),R(4)],                },
		]);
		DoTigerT322();
	}
	internal void DoTigerT322()
	{
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
		t = t/**/.h("S", d: '(').h("L", d: ',');
		t = t/**/	.h("L", d: ',').h("S", 1, 6, '(');
		t = t/**/						.h("L", d: ',').H("S", d: 'a').N("S", d: 'b').uu();
		t = t/**/			.n("S", 7, 20, '(');
		t = t/**/				.h("L", d: ',').h("S", 8, 17, '(');
		t = t/**/									.h("L", d: ',').H("S", d: 'a');
		t = t/**/										.n("S", 11, 16, '(').h("L", d: ',');
		t = t/**/											.H("S", d: 'b').N("S", d: 'a').uuuu();
		t = t/**/								.N("S", d: 'b').uuu();
		t = t/**/	.n("S", 21, 26, '(').h("L", d: ',');
		t = t/**/						.H("S", d: 'a').N("S", d: 'b').uuuuuU();
	}

	[TestMethod]
	public void Dragon2F451()
	{
		NewSer("Z=S S=iSeS S=iS S=a", ['i', 'e', 'a', 'b', 'c', '\0'], ['S'], [
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [1] },
			new() { modes = [-1,  -1, -1, -1, -1,R(0)],             },
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [4] },
			new() { modes = [-1,R(3), -1, -1, -1,R(3)],             },
			new() { modes = [-1,   5, -1, -1, -1,R(2)],             },
			new() { modes = [ 2,  -1,  3,  3,  3,  -1], pushs = [6] },
			new() { modes = [-1,R(1), -1, -1, -1,R(1)],             },
		]);
		DoDragon2F451();
	}
	internal void DoDragon2F451()
	{
		True("b"); True("ia"); True("iaeb");
		True("iia"); True("iiaeb"); True("iaeib");
		True("iiaebec"); True("iaeibec");
		True("iiia"); True("iiiaeb"); True("iiaeib"); True("iaeiib");
		True("iiiaebec"); True("iiaeibec"); True("iiaebeic");
		True("iaeiibec"); True("iaeibeic");
		True("iiiaebecea"); True("iiaeibecea"); True("iaeiibecea"); True("iaeibeiaec");
		var t = Parse("iiiaebec");
		t = t/**/.h(d: 'i').h(d: 'i').h(d: 'i');
		t = t/**/						.H(d: 'a').N(d: 'b').u();
		t = t/**/					.N(d: 'c').uuuU();
		t = Parse("iiaebeic");
		t = t/**/.h(d: 'i').h(d: 'i');
		t = t/**/				.H(d: 'a').N(d: 'b').u();
		t = t/**/			.n(d: 'i').H(d: 'c').uuuU();
		t = Parse("iiaeibecea");
		t = t/**/.h(d: 'i').h(d: 'i');
		t = t/**/				.H(d: 'a').n(d: 'i');
		t = t/**/					.H(d: 'b').N(d: 'c').uu();
		t = t/**/			.N(d: 'a').uuU();
		t = Parse("iaeiibecea");
		t = t/**/.h(d: 'i').H(d: 'a');
		t = t/**/			.n(d: 'i').h(d: 'i');
		t = t/**/						.H(d: 'b').N(d: 'c').u();
		t = t/**/					.N(d: 'a').uuuU();
	}

	[TestMethod]
	public void Dragon2F449()
	{
		NewSer("Z-E E=E+E E=E*E E-(E) E=a", ['a', 'b', '+', '*', '(', ')', '\0'], ['E'], [
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [1] },
			new() { modes = [-1, -1,   4,   5, -1,  -1,R(0)],             },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [6] },
			new() { modes = [-1, -1,R(4),R(4), -1,R(4),R(4)],             },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [7] },
			new() { modes = [ 3,  3,  -1,  -1,  2,  -1,  -1], pushs = [8] },
			new() { modes = [-1, -1,   4,   5, -1,   9,  -1],             },
			new() { modes = [-1, -1,R(1),   5, -1,R(1),R(1)],             },
			new() { modes = [-1, -1,R(2),R(2), -1,R(2),R(2)],             },
			new() { modes = [-1, -1,R(3),R(3), -1,R(3),R(3)],             },
		]); // no E'->E
		DoDragon2F449();
	}
	internal void DoDragon2F449()
	{
		True("a"); True("a+b"); True("a*b");
		True("(b)"); True("(b*a)"); True("(b+a)"); True("(a)*b"); True("a+(b)");
		True("a+b+a"); True("a*b*a"); True("a+b*a"); True("b*a+b");
		True("(a)+a+a"); True("b*(b)*b"); True("a+(b*a+b)*b");
		True("(a)+(a*a)"); True("(b*(b))*b"); True("a+((b*a+b)*b)");
		False("+"); False("a+"); False("*b"); False("a++b");
		False("(b)a"); False("a*((b"); False("b+a)"); False("(a*)+b");
		var t = Parse("a+b*((b*a+b)*b)");
		t = t/**/.Eq(d: '+').H(d: 'a');
		t = t/**/		.n("E", 2, 15, '*').H(d: 'b');
		t = t/**/						.n("E", 5, 14, '*').h("E", 6, 11, '+');
		t = t/**/												.h(d: '*').H(d: 'b').N(d: 'a').u();
		t = t/**/												.N(d: 'b').u();
		t = t/**/											.N(d: 'b').uuuU();
	}
}
