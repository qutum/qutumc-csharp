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
		if (err < 0 && d is string aim && s.t.info is string test) {
			var ts = test.Split(SerMaker<char, string>.ErrMore);
			var As = aim.Split("  ");
			if ((As.Length < 2 ? ts.Length != As.Length : ts.Length < As.Length)
				|| As.Zip(ts).Any(ea =>
					!ea.First.Split(' ').ToHashSet().IsSubsetOf(ea.Second.Split(' ').ToHashSet())))
				Fail($"Expected Err <{aim}> Actual <{test.Replace("\n", "  ")}>");
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

	public void NewSer(string Alts, char[] keys, ushort[] names, (short[] modes, short[] pushs)[] forms)
	{
		var alts = Alts.Split('\n', ' ').Select(a => new SynAlt<string> {
			name = a[0..1],
			size = (short)(a.Length - 2),
			lex = (short)(a.IndexOfAny(keys, 2) - 2),
			synt = a[1] switch { '+' => 1, '-' => -1, _ => 0 },
		}).ToArray();
		ushort[] ks = [0, .. keys];
		ushort[] ns = [0, .. names];
		var Fs = new SynForm[forms.Length];
		foreach (var (f, fx) in forms.Each())
			if (f.modes != null) {
				var F = Fs[fx] = new();
				F.pushs.s = [];
				Array.Sort(F.modes.x = ks[..], F.modes.s = [-1, .. f.modes], 1, keys.Length);
				if (f.pushs != null)
					Array.Sort(F.pushs.x = ns[..], F.pushs.s = [-1, .. f.pushs]);
			}
		ser = new(name => name[0], alts, Fs) { dump = 3 };
	}
	public static short _ = -1;
	public static short r0 = R(0), r1 = R(1), r2 = R(2), r3 = R(3), r4 = R(4),
						r5 = R(5), r6 = R(6), r7 = R(7), r8 = R(8), r9 = R(9);
	public static short R(int alt) => SynForm.Reduce(alt);

	public void True(string read) => IsTrue(ser.Begin(new LerStr(read)).Check());
	public void False(string read) => IsFalse(ser.Begin(new LerStr(read)).Check());

	public Ser Parse(string read) => (ser.Begin(new LerStr(read)).Parse().Dump(), ser);

	[TestMethod]
	public void TigerF325()
	{
		NewSer("S=E E=T+E E=T T=a",
			['a', 'b', '+', '\0'], ['E', 'T'], [ (null, null),
			( [ 5, 5,  _,  _ ], [ 2, 3 ] ),
			( [ _, _,  _, r0 ],     null ),
			( [ _, _,  4, r2 ],     null ),
			( [ 5, 5,  _,  _ ], [ 6, 3 ] ),
			( [ _, _, r3, r3 ],     null ),
			( [ _, _,  _, r1 ],     null ),
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
		NewSer("Z=S S=V=E S-E E-V V=a V=*E",
			['a', 'b', '*', '=', '\0'], ['S', 'E', 'V'], [ (null, null),
			( [ 8, 8, 6,  _,  _ ], [ 2, 5, 3 ] ),
			( [ _, _, _,  _, r0 ],        null ),
			( [ _, _, _,  4, r3 ],        null ),
			( [ 8, 8, 6,  _,  _ ], [ _, 9, 7 ] ),
			( [ _, _, _,  _, r2 ],        null ),
			( [ 8, 8, 6,  _,  _ ], [ _,10, 7 ] ),
			( [ _, _, _, r3, r3 ],        null ),
			( [ _, _, _, r4, r4 ],        null ),
			( [ _, _, _,  _, r1 ],        null ),
			( [ _, _, _, r5, r5 ],        null ),
		]);
		DoTigerT328();
	}
	internal void DoTigerT328()
	{
		True("a"); True("*b"); True("**a");
		True("a=b"); True("a=*b"); True("b=**a");
		True("*a=b"); True("*b=*a"); True("*a=**b");
		True("**a=b"); True("**a=*b"); True("**b=**a");
		False("*"); False("a*"); False("*a*");
		False("a="); False("=b"); False("*="); False("=*");
		False("*a="); False("a*="); False("=*b"); False("=b*");
		False("a*=*b"); False("*b=a*"); False("*a*=b");
		False("a==b"); False("*a==*b");
		var t = Parse("**a");
		t = t/**/.Eq("Z", 0, 3).h("V", d: '*').h("V", d: '*').H("V", d: 'a').uuuU();
		t = Parse("**b=***a").Eq("Z", 0, 8);
		t = t/**/.h("S", d: '=').h("V", d: '*').h("V", d: '*').H("V", d: 'b').uu();
		t = t/**/				.n("V", d: '*').h("V", d: '*').h("V", d: '*').H("V", d: 'a').uuuuuU();
	}

	[TestMethod]
	public void TigerT322()
	{
		NewSer("Z=S S=(L) S=a L-S L=L,S",
			['(', ')', 'a', 'b', ',', '\0'], ['S', 'L'], [ (null, null),
			( [  3,  _,  2,  2,  _,  _ ], [ 4, _ ] ),
			( [ r2, r2, r2, r2, r2, r2 ],     null ),
			( [  3,  _,  2,  2,  _,  _ ], [ 7, 5 ] ),
			( [  _,  _,  _,  _,  _, r0 ],     null ),
			( [  _,  6,  _,  _,  8,  _ ],     null ),
			( [ r1, r1, r1, r1, r1, r1 ],     null ),
			( [ r3, r3, r3, r3, r3, r3 ],     null ),
			( [  3,  _,  2,  2,  _,  _ ], [ 9, _ ] ),
			( [ r4, r4, r4, r4, r4, r4 ],     null ),
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
		NewSer("Z=S S=iSeS S=iS S=a",
			['i', 'e', 'a', 'b', 'c', '\0'], ['S'], [
			( [ 2,  _, 3, 3, 3,  _ ], [ 1 ] ),
			( [ _,  _, _, _, _, r0 ],  null ),
			( [ 2,  _, 3, 3, 3,  _ ], [ 4 ] ),
			( [ _, r3, _, _, _, r3 ],  null ),
			( [ _,  5, _, _, _, r2 ],  null ),
			( [ 2,  _, 3, 3, 3,  _ ], [ 6 ] ),
			( [ _, r1, _, _, _, r1 ],  null ),
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
		NewSer("Z-E E=E+E E=E*E E-(E) E=a",
			['a', 'b', '+', '*', '(', ')', '\0'], ['E'], [
			( [ 3, 3,  _,  _, 2,  _,  _ ], [ 1 ] ),
			( [ _, _,  4,  5, _,  _, r0 ],  null ),
			( [ 3, 3,  _,  _, 2,  _,  _ ], [ 6 ] ),
			( [ _, _, r4, r4, _, r4, r4 ],  null ),
			( [ 3, 3,  _,  _, 2,  _,  _ ], [ 7 ] ),
			( [ 3, 3,  _,  _, 2,  _,  _ ], [ 8 ] ),
			( [ _, _,  4,  5, _,  9,  _ ],  null ),
			( [ _, _, r1,  5, _, r1, r1 ],  null ),
			( [ _, _, r2, r2, _, r2, r2 ],  null ),
			( [ _, _, r3, r3, _, r3, r3 ],  null ),
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
