//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Linq;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Kord = char;
using Nord = ushort;

public readonly struct Ser(SyntStr t, SynterStr s)
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0305:Simplify collection initialization")]
	public readonly Ser Eq(string name = null, object d = null, Jov<int?> j = default, int err = 0)
	{
		AreNotEqual(null, t);
		AreEqual(err, t.err);
		if (name != null) AreEqual(name, t.name);
		if (j.on != null) AreEqual(j.on, t.j.on);
		if (j.via != null) AreEqual(j.via, t.j.via);
		if (err != 0 && d is string aim && t.info?.ToString() is string test) {
			var ts = test.Split(SerMaker<char, string>.ErrMore);
			var As = aim.Split("  ");
			if ((As.Length < 3 ? ts.Length != As.Length : ts.Length < As.Length)
				|| !As.Zip(ts).All(ea => ea.First.Split(' ').ToCount().ToHashSet()
					.IsSubsetOf(ea.Second.Split(' ').ToCount().ToHashSet())))
				Fail($"Expected Error <{aim}> Actual <{test.Replace("\n", "  ")}>");
		}
		else if (d != null) AreEqual(d,
			d is string && t.err == 0 ? s.ler.Lexs(t.j) : t.info);
		return this;
	}

	public Ser h(string name = null, object d = null, Jov<int?> j = default, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Vine;
	public Ser n(string name = null, object d = null, Jov<int?> j = default, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Vine;

	public Ser H(string name = null, object d = null, Jov<int?> j = default, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Leaf;
	public Ser N(string name = null, object d = null, Jov<int?> j = default, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Leaf;

	public readonly Ser Vine { get { AreNotEqual(null, t.head); return this; } }
	public readonly Ser Leaf { get { AreEqual(null, t.head); return this; } }
	public Ser u { get { AreEqual(null, t.next); AreNotEqual(null, t.up); return new Ser(t.up, s); } }
	public Ser U { get { AreEqual(null, t.next); AreEqual(null, t.up); return new Ser(t.up, s); } }
	public Ser uu => u.u;
	public Ser uU => u.U;
	public Ser uuu => u.u.u;
	public Ser uuU => u.u.U;
	public Ser uuuu => u.u.u.u;
	public Ser uuuU => u.u.u.U;
	public Ser uuuuU => u.u.u.u.U;
	public Ser uuuuuU => u.u.u.u.u.U;
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Use();

	public void Dispose() => env.Dispose();

	internal SynterStr ser;

	void NewSer(string Alts, Kord[] keys, Nord[] names, (short[] goKs, short[] goNs)[] forms)
	{
		var alts = Alts.Split('\n', ' ').Select(a => new SynAlt<string> {
			name = a[0..1],
			size = (short)(a.Length - 2),
			lex = (short)(a.IndexOfAny(keys, 2) - 2),
			synt = a[1] switch { '+' => 1, '-' => -1, _ => 0 },
			syntName = a[0..1],
		}).ToArray();
		var Fs = new SynForm[forms.Length];
		foreach (var (f, fx) in forms.Each())
			if (f.goKs != null) {
				var F = Fs[fx] = new();
				F.goNs.s = [];
				Array.Sort(F.goKs.x = keys[..], F.goKs.s = f.goKs[..]);
				if (f.goNs != null)
					Array.Sort(F.goNs.x = names[..], F.goNs.s = f.goNs[..]);
			}
		ser = new(name => name[0], alts, Fs) { dump = 3 };
	}
	const short _ = -1;
	static readonly short r0 = R(0), r1 = R(1), r2 = R(2), r3 = R(3), r4 = R(4), r5 = R(5);
	static short R(int alt) => SynForm.Redu(alt);

	public void True(string read) => IsTrue(Begin(read).Check());
	public void False(string read) => IsFalse(Begin(read).Check());
	public Ser Parse(string read) => new(Begin(read).Parse().Dump(), ser);

	private SynterStr Begin(string read)
	{
		using (var env = EnvWriter.Use()) env.WriteLine(read);
		return (SynterStr)ser.Begin(new LerStr(read));
	}

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
		var _ = Parse("a+b+a");
		_ = _/**/.Eq("S", j: (0, 5)).h("E");
		_ = _/**/				.H("T", 'a', (0, 1));
		_ = _/**/				.n("E", '+', (2, 5)).H("T", 'b', (2, 3));
		_ = _/**/									.n("E").H("T", 'a', (4, 5)).uuuuU;
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
		var _ = Parse("**a");
		_ = _/**/.Eq("Z", j: (0, 3)).h("V", '*').h("V", '*').H("V", 'a').uuuU;
		_ = Parse("**b=***a").Eq("Z", j: (0, 8));
		_ = _/**/.h("S", '=').h("V", '*').h("V", '*').H("V", 'b').uu;
		_ = _/**/				.n("V", '*').h("V", '*').h("V", '*').H("V", 'a').uuuuuU;
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
		var _ = Parse("((a,b),((a,(b,a)),b),(a,b))").Eq("Z", j: (0, 27));
		_ = _/**/.h("S", '(').h("L", ',');
		_ = _/**/	.h("L", ',').h("S", '(', (1, 6));
		_ = _/**/						.h("L", ',').H("S", 'a').N("S", 'b').uu;
		_ = _/**/			.n("S", '(', (7, 20));
		_ = _/**/				.h("L", ',').h("S", '(', (8, 17));
		_ = _/**/									.h("L", ',').H("S", 'a');
		_ = _/**/										.n("S", '(', (11, 16)).h("L", ',');
		_ = _/**/											.H("S", 'b').N("S", 'a').uuuu;
		_ = _/**/								.N("S", 'b').uuu;
		_ = _/**/	.n("S", '(', (21, 26)).h("L", ',');
		_ = _/**/						.H("S", 'a').N("S", 'b').uuuuuU;
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
		var _ = Parse("iiiaebec");
		_ = _/**/.h(d: 'i').h(d: 'i').h(d: 'i');
		_ = _/**/						.H(d: 'a').N(d: 'b').u;
		_ = _/**/					.N(d: 'c').uuuU;
		_ = Parse("iiaebeic");
		_ = _/**/.h(d: 'i').h(d: 'i');
		_ = _/**/				.H(d: 'a').N(d: 'b').u;
		_ = _/**/			.n(d: 'i').H(d: 'c').uuuU;
		_ = Parse("iiaeibecea");
		_ = _/**/.h(d: 'i').h(d: 'i');
		_ = _/**/				.H(d: 'a').n(d: 'i');
		_ = _/**/					.H(d: 'b').N(d: 'c').uu;
		_ = _/**/			.N(d: 'a').uuU;
		_ = Parse("iaeiibecea");
		_ = _/**/.h(d: 'i').H(d: 'a');
		_ = _/**/			.n(d: 'i').h(d: 'i');
		_ = _/**/						.H(d: 'b').N(d: 'c').u;
		_ = _/**/					.N(d: 'a').uuuU;
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
		var _ = Parse("a+b*((b*a+b)*b)");
		_ = _/**/.Eq(d: '+').H(d: 'a');
		_ = _/**/		.n("E", '*', (2, 15)).H(d: 'b');
		_ = _/**/						.n("E", '*', (5, 14)).h("E", '+', (6, 11));
		_ = _/**/												.h(d: '*').H(d: 'b').N(d: 'a').u;
		_ = _/**/												.N(d: 'b').u;
		_ = _/**/											.N(d: 'b').uuuU;
	}
}
