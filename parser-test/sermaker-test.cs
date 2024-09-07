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
using System.Collections.Generic;
using System.Linq;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Gram = SynGram<char, string>;

file static class Extension
{
	public static void Eq(this (bool empty, IEnumerable<char> first) test, bool empty, string first)
	{
		AreEqual(empty, test.empty);
		AreEqual(first, string.Join(" ", test.first.Order()));
	}

	public static void Eq(this Dictionary<SerMaker<char, string>.Clash, (HashSet<char> ks, short m)> tests,
		params (HashSet<char>, short[] redus, short[] shifts)[] aims)
	{
		AreEqual(tests.Count, aims.Length);
		foreach (var (ks, redus, shifts) in aims) {
			IsTrue(tests.TryGetValue(new() {
				redus = redus.Select(a => (short)(a ^ a >> 15)).ToHashSet(),
				shifts = shifts?.Select(a => (short)(a ^ a >> 15)).ToHashSet()
			}, out var t));
			IsTrue(ks.SetEquals(t.ks));
			AreEqual(shifts?.Min() < 0 ? 0 : redus.Min() < 0 ? SynForm.Reduce(~redus.Min()) : -1,
				Math.Min((int)t.m, 0));
		}
	}
}

[TestClass]
public class TestSerMaker : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	SerMaker<char, string> mer;
	TestSynter ser;

	public void NewMer(Gram gram)
	{
		mer = new(gram, k => k, n => n[0], (_) => { });
	}

	public void NewSer(bool recover = false)
	{
		var (a, f, r) = mer.Make(out var _);
		ser = new TestSynter {
			ser = new(n => n[0], a, f, r) { recover = recover, dumper = mer.Dumper }
		};
	}

	[TestMethod]
	public void FirstTigerG312()
	{
		NewMer(new Gram().n("S")["Z"]
			.n("Z")['d']["X", "Y", "Z"]
			.n("Y")[[]]['c']
			.n("X")["Y"]['a']
		);
		mer.Firsts();
		mer.First("X").Eq(true, "a c");
		mer.First("Y").Eq(true, "c");
		mer.First("Z").Eq(false, "a c d");
	}

	[TestMethod]
	public void FirstTigerT316()
	{
		NewMer(new Gram()
			.n("S")["E"]
			.n("E")["T", "e"]
			.n("e")['+', "T", "e"]['-', "T", "e"][[]]
			.n("T")["F", "t"]
			.n("t")['*', "F", "t"]['/', "F", "t"][[]]
			.n("F")['a']['1']['(', "E", ')']
		);
		mer.Firsts();
		mer.First("S").Eq(false, "( 1 a");
		mer.First("E").Eq(false, "( 1 a");
		mer.First("e").Eq(true, "+ -");
		mer.First("T").Eq(false, "( 1 a");
		mer.First("t").Eq(true, "* /");
		mer.First("F").Eq(false, "( 1 a");
	}

	[TestMethod]
	public void Clash1()
	{
		NewMer(new Gram().n("S")["E"].n("E")['i']["E", "E"]['j']);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['i'], [2], [1]),
			(['j'], [2], [3])
		);
		NewMer(new Gram().n("S")["E"].n("E")['i']["E", "E"]['j'].clash);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['i'], [2], [1]),
			(['j'], [2], [3])
		);
		NewMer(new Gram().n("S")["E"].n("E")['i'].clash["E", "E"].clash['j'].clash);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['i'], [~2], [1]),
			(['j'], [2], [~3])
		);
	}

	[TestMethod]
	public void Clash2()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", "E"].clash
					["E", '+', "E"].clash
					['x'].clash
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['x'], [1], [~3]),
			(['x'], [2], [~3])
		);
		NewMer(new Gram().n("S")["E"]
			.n("E")['x'].clash
					["E", "E"].clash
					["E", '+', "E"].clash
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['+'], [2], [~3]),
			(['+'], [~3], [3]),
			(['x'], [~2], [1]),
			(['x'], [~3], [1])
		);
	}

	[TestMethod]
	public void Clash3()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", ',', "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
					['a']['b']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			([','], [~1], [1]),
			([','], [~2], [1]),
			([','], [~3], [1]),
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['+'], [~3], [2]),
			(['^'], [1], [~3]),
			(['^'], [2], [~3]),
			(['^'], [3], [~3])
		);
	}

	[TestMethod]
	public void Clash4()
	{
		NewMer(new Gram().n("S")["E"]
			.n("B")[',', "E"].clash
					['+', "E"].clash
					['^', "E"].clashRight
			.n("E")["E", "B"]
					['a']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			([','], [~1], [1]),
			([','], [~2], [1]),
			([','], [~3], [1]),
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['+'], [~3], [2]),
			(['^'], [1], [~3]),
			(['^'], [2], [~3]),
			(['^'], [3], [~3])
		);
		NewMer(new Gram().n("S")["E"]
			.n("B")["E", ',']
					["E", '+']
					["E", '^']
			.n("E")["B", "E"]
					['a']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq( // no way
			([','], [4], [1]),
			(['+'], [4], [2]),
			(['^'], [4], [3])
		);
	}

	[TestMethod]
	public void Clash5()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", '?', "E", ':', "E", '?', "E"].clashRight
					["E", '+', "E"].clash
					["E", '$', "E"].clash
					["E", '$'].clash
					['a']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['?', ':'], [1], [~1]),
			(['?'], [~2], [1]),
			(['?'], [~3], [1]),
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['+'], [~3], [2]),
			(['$'], [1], [3, ~4]),
			(['$'], [2], [3, ~4]),
			(['$'], [3], [3, ~4])
		);
	}

	[TestMethod]
	public void Clash6()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", '+', "E"].clash
					["E", '-', "E"].clashPrev
					["E", '|', "E"].clashPrev
					["E", '*', "E"].clash
					["E", '/', "E"].clashPrev
					['a']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['+'], [~1], [1]),
			(['+'], [~2], [1]),
			(['+'], [~3], [1]),
			(['+'], [~4], [1]),
			(['+'], [~5], [1]),
			(['-'], [~1], [2]),
			(['-'], [~2], [2]),
			(['-'], [~3], [2]),
			(['-'], [~4], [2]),
			(['-'], [~5], [2]),
			(['|'], [~1], [3]),
			(['|'], [~2], [3]),
			(['|'], [~3], [3]),
			(['|'], [~4], [3]),
			(['|'], [~5], [3]),
			(['*'], [1], [~4]),
			(['*'], [2], [~4]),
			(['*'], [3], [~4]),
			(['*'], [~4], [4]),
			(['*'], [~5], [4]),
			(['/'], [1], [~5]),
			(['/'], [2], [~5]),
			(['/'], [3], [~5]),
			(['/'], [~4], [5]),
			(['/'], [~5], [5])
		);
	}

	[TestMethod]
	public void Clash7()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", ",", "E"].clash
			.n("B")['+', "E"].clash
					['^', "E"].clashRight
			.n(",")[','].clash
			.n("E")["E", "B"]
					['a']['b']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			([','], [1], [~4]),
			([','], [2], [~4]),
			([','], [3], [~4]),
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['+'], [~3], [2]),
			(['^'], [1], [~3]),
			(['^'], [2], [~3]),
			(['^'], [3], [~3])
		);
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", "B", "E"]
			.n("B")[',']
					['+']
					['^']
			.n("E")['a']['b']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq( // no way
			([','], [1], [2]),
			(['+'], [1], [3]),
			(['^'], [1], [4])
		);
	}

	[TestMethod]
	public void ClashTigerF332()
	{
		NewMer(new Gram()
			.n("P")["L"]
			.n("S")['i', '=', 'i']
					['*', 'i', ':', "S"]
					['(', "S", ')']
					['?', 'i', '&', "S"].clash
					['?', 'i', '&', "S", '|', "S"].clash
			.n("L")["S"]["L", ';', "S"]
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq((['|'], [4], [~5]));
	}

	[TestMethod]
	public void ClashTigerF337()
	{
		NewMer(new Gram().n("Z")["S"]
			.n("S")['i', ':', "A"]['i', ':', "B"]['i', ':', "C"]
			.n("B")["B", '|', "B"].clash["B", '&', "B"].clash
					["A", '=', "A"]
					['i'].clash
			.n("A")["A", '^', "A"].clashRight
					['i'].clash
			.n("C")['i'].clash["D"]
			.n("D")['i'].clash
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['|'], [~4], [4]),
			(['|'], [~5], [4]),
			(['&'], [4], [~5]),
			(['&'], [~5], [5]),
			(['^'], [8], [~8]),
			(['\0'], [7, 9, 10, ~12], null)
		);
	}

	[TestMethod]
	public void MakeTigerF325()
	{
		NewMer(new Gram()
			.n("S")["E"]
			.n("E")["T", '+', .., "E"]["T"]
			.n("T")['a', ..]['b', ..]
		);
		NewSer();
		ser.DoTigerF325();
	}

	[TestMethod]
	public void MakeTigerT328()
	{
		NewMer(new Gram()
			.n("Z")["S"]
			.n("S")["V", '=', .., "E"]["E"].syntOmit
			.n("E")["V"].syntOmit
			.n("V")['a', ..]['b', ..]['*', .., "E"]
		);
		NewSer();
		ser.DoTigerT328();
	}

	[TestMethod]
	public void MakeTigerT322()
	{
		NewMer(new Gram()
			.n("Z")["S"]
			.n("S")['(', .., "L", ')']['a', ..]['b', ..]
			.n("L")["S"].syntOmit["L", ',', .., "S"]
		);
		NewSer();
		ser.DoTigerT322();
	}

	[TestMethod]
	public void MakeDragon2F451()
	{
		NewMer(new Gram()
			.n("Z")["S"]
			.n("S")['i', .., "S"].clash['i', .., "S", 'e', "S"].clash
					['a', ..]['b', ..]['c', ..]
		);
		NewSer();
		ser.DoDragon2F451();
	}

	[TestMethod]
	public void MakeDragon2F449()
	{
		NewMer(new Gram().n("Z")["E"].syntOmit
			.n("E")["E", '+', .., "E"].clash
					["E", '*', .., "E"].clash
					['(', .., "E", ')'].syntOmit
					['a', ..]['b', ..]
		);
		NewSer();
		ser.DoDragon2F449();
	}

	[TestMethod]
	public void Synt1()
	{
		NewMer(new Gram()
			.n("S")["E"].n("E")["A", "B"]
			.n("A")['a', ..].synt
			.n("B")['b', ..].synt
		);
		NewSer(); ser.ser.synt = false;
		ser.Parse("ab").Eq("A", d: 'a').Leaf().N("B", d: 'b').U();
	}

	[TestMethod]
	public void Synt2()
	{
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", '+', .., "E"].clash.syntLeft
					["E", '-', .., "E"].clashPrev.syntLeft
					["E", '*', .., "E"].clash.syntLeft
					["E", '/', .., "E"].clashPrev.syntLeft
					['#', .., "T"]
					['#', .., "T", "E"].clash.syntRight
					["T"].syntOmit
			.n("T")["W"].syntOmit["N"].syntOmit
			.n("W")['a', ..]['b', ..]
			.n("N")['1', ..]['2', ..]
		);
		NewSer();
		var t = ser.Parse("a+1").Eq("S");
		t = t/**/	.H("W", d: 'a');
		t = t/**/	.n("E", d: '+').H("N", d: '1').uuU();
		t = ser.Parse("a+1-b");
		t = t/**/	.H("W", d: 'a');
		t = t/**/	.n("E", d: '+').H("N", d: '1').u();
		t = t/**/	.n("E", d: '-').H("W", d: 'b').uuU();
		t = ser.Parse("2*b-b+1/a");
		t = t/**/	.H("N", d: '2');
		t = t/**/	.n("E", d: '*').H("W", d: 'b').u();
		t = t/**/	.n("E", d: '-').H("W", d: 'b').u();
		t = t/**/	.n("E", d: '+').H("N", d: '1').n("E", d: '/').H("W", d: 'a').uuuU();
		t = ser.Parse("1+#a#1#b*2");
		t = t/**/.H("N", d: '1').n("E", d: '+');
		t = t/**/					.h("E", d: '#').H("W", d: 'a').u();
		t = t/**/					.n("E", d: '#').H("N", d: '1').u();
		t = t/**/					.n("E", d: '#').H("W", d: 'b').u();
		t = t/**/					.n("E", d: '*').H("N", d: '2').uuuU();
	}

	[TestMethod]
	public void ErrorCyclic()
	{
		NewMer(new Gram()
			.n("Z")["S", 'a'].clash.syntOmit
			.n("S")['a', ..]["S"].clash
		);
		NewSer();
		ser.Parse("aa").Eq(err: -1).h(null, 0, 1, "cyclic grammar", -2).uU();
	}

	[TestMethod]
	public void Error1()
	{
		NewMer(new Gram().n("S")['a']);
		NewSer();
		ser.Parse("").H(null, 0, 0, "S a", -1).uU();
		ser.Parse("b").H(null, 0, 1, "S a", -1).uU();
		NewMer(new Gram().n("Z")["S"].n("S")['a']['b']);
		NewSer();
		ser.Parse("").H(null, 0, 0, "Z S  S b", -1).uU();
		ser.Parse("c").H(null, 0, 1, "Z S  S b", -1).uU();
		ser.Parse("aa").H(null, 1, 2, "end of", -1).uU();
	}

	[TestMethod]
	public void Error2()
	{
		NewMer(new Gram().n("P")["S"].syntOmit
			.n("S", "sentence")
					["E"]["S", ';', "E"]
			.n("E", "expression")
					["A"].labelLow["B"].labelLow
					['(', .., "E", ')'].label("parenth")
					['a']
			.n("A")["E", '+', .., "E"].clash.label("addition")
			.n("B")["E", '-', .., "E"].clash.label("subtraction")
		);
		NewSer();
		ser.Parse("aa").H(null, 1, 2, "sentence ;", -1).uU();
		ser.Parse("-a").H(null, 0, 1, "sentence expression  sentence sentence", -1).uU();
		ser.Parse("a+").H(null, 2, 2, "addition expression", -1).uU();
		ser.Parse("a++a").H(null, 2, 3, "addition expression", -1).uU();
		ser.Parse("a+a--a").H(null, 4, 5, "subtraction expression", -1).uU();
		ser.Parse(")").H(null, 0, 1, "sentence expression  sentence sentence", -1).uU();
		ser.Parse("a)").H(null, 1, 2, "sentence ;", -1).uU();
		ser.Parse("(a))").H(null, 3, 4, "sentence ;", -1).uU();
		ser.Parse("(").H(null, 1, 1, "parenth expression", -1).uU();
		ser.Parse("(a").H(null, 2, 2, "parenth )", -1).uU();
		ser.Parse("(a;").H(null, 2, 3, "parenth )", -1).uU();
		ser.Parse(";a").H(null, 0, 1, "sentence expression  sentence sentence", -1).uU();
		ser.Parse("a;;").H(null, 2, 3, "sentence expression", -1).uU();
		ser.Parse("(a;a)").H(null, 2, 3, "parenth )", -1).uU();
	}

	[TestMethod]
	public void Recover1()
	{
		NewMer(new Gram().n("Z")["S"]
			.n("S", "sen")["E", ';'].recover["E", ';', "S"]
			.n("E", "exp")['a', ..]['b', ..]
		);
		NewSer(true);
		ser.True("a;"); ser.True("b;a;"); ser.False("a;a");
		ser.Parse("").H(null, 0, 0, "sen exp  sen exp", -1).uU();
		ser.Parse(";").H("S", 0, 1, "sen exp  sen exp", 1).u();
		var t = ser.Parse("aa").Eq("Z");
		t = t/**/	.h("S", 0, 2, "sen ;  sen ;", 1).H("E", 0, 1, 'a').uu();
		t = t/**/.n(err: -1).H(null, 1, 2, "sen ;  sen ;", -1).uU();
		t = ser.Parse("a;;b;;").Eq("Z");
		t = t/**/	.H("S", 0, 6, d: "5:6! end of", 1).u();
		t = t/**/.n(err: -1).H(null, 2, 3, "end of", -1);
		t = t/**/			.N(null, 3, 4, "end of", -1).N(null, 5, 6, "end of", -1).uU();
		t = ser.Parse(";;b;;").Eq("Z");
		t = t/**/	.H("S", 0, 5, d: "4:5! end of", 1).u();
		t = t/**/.n(err: -1).H(null, 0, 1, "sen exp  sen", -1).N(null, 1, 2, "end of", -1);
		t = t/**/			.N(null, 2, 3, "end of", -1).N(null, 4, 5, "end of", -1).uU();
	}

	[TestMethod]
	public void Recover2()
	{
		NewMer(new Gram().n("Z", "prog")["S"]
			.n("S", "sen")["E", ';'].recover["S", "E", ';'].recover
			.n("E", "exp")['a', ..]['b', ..]
		);
		NewSer(true);
		ser.Parse("a;").h("S").H("E", d: 'a').uuU();
		ser.Parse("a;b;").h("S").h("S").H("E", d: 'a').u().N("E", d: 'b').uuU();
		ser.Parse("").H(null, 0, 0, "prog sen  sen exp", -1).uU();
		ser.Parse(";").H("S", 0, 1, "prog sen  sen exp", 1).u();
		var t = ser.Parse("aa").Eq("Z");
		t = t/**/	.h("S", 0, 2, "sen ;", 1).H("E", 0, 1, 'a').uu();
		t = t/**/.n(err: -1).H(null, 1, 2, "sen ;", -1).uU();
		t = ser.Parse("a;b").Eq("Z");
		t = t/**/	.h("S", 0, 3, d: "sen ;", 1).h("S", 0, 2).H("E", 0, 1, 'a').u();
		t = t/**/								.N("E", 2, 3, 'b').uu();
		t = t/**/.n(err: -1).H(null, 3, 3, "sen ;", -1).uU();
		t = ser.Parse(";;a;;").Eq("Z");
		t = t/**/	.h("S", 0, 5, d: "4:5! sen exp", 1);
		t = t/**/		.h("S", 0, 4).h("S", 0, 2, "1:2! sen exp", 1);
		t = t/**/						.H("S", 0, 1, "0:1! prog sen  sen exp", 1).u();
		t = t/**/					.N("E", 2, 3, 'a').uuu();
		t = t/**/.n(err: -1).H(null, 0, 1, "prog sen  sen exp", -1);
		t = t/**/			.N(null, 1, 2, "sen exp", -1);
		t = t/**/			.N(null, 4, 5, "sen exp", -1).uU();
	}

	void DoRecover3()
	{
		NewMer(new Gram().n("Z", "prog")["S"]
			.n("S", "sen")["B"].syntOmit
					["E", ';'].recover["S", "E", ';'].recover
			.n("B", "blo")['{', .., "S", '}'].recover.syntOmit
			.n("E", "exp")['a', ..]['b', ..]
					["E", '+', .., "E"].clash.label("add")
					["E", '*', .., "E"].clash.label("mul")
					['(', .., "E", ')'].recover.label("par")
					['(', .., "B", ')'].recover.label("plo")
		);
		NewSer(true);
	}
	[TestMethod]
	public void Recover3()
	{
		DoRecover3();
		ser.True("a;"); ser.True("a;b;"); ser.True("a;b;a;");
		ser.True("(a);"); ser.True("{a*b;}"); ser.True("{a;b;}"); ser.True("({a;b;});");
		var t = ser.Parse("a+b){;b;");
		t = t/**/	.h("S").h("S", 0, 6, d: "sen ;", 1);
		t = t/**/				.h("E", 0, 3, d: '+').H(d: 'a').N(d: 'b').uu();
		t = t/**/			.N("E", 6, 7, 'b').uu();
		t = t/**/.n(err: -1).H(null, 3, 4, "sen ;", -1).uU();
		t = ser.Parse("(a*b;");
		t = t/**/	.h("S").h("E", 0, 4, d: "par )", 1);
		t = t/**/				.h("E", 1, 4, d: '*').H(d: 'a').N(d: 'b').uuuu();
		t = t/**/.n(err: -1).H(null, 4, 4, "par )", -1).uU();
		t = ser.Parse("{a*b;");
		t = t/**/	.h("S", 1, 5).h("E", 1, 4, d: '*').H(d: 'a').N(d: 'b').uu();
		t = t/**/	.N("B", 0, 5, "sen exp  blo }", 1).u();
		t = t/**/.n(err: -1).H(null, 5, 5, "sen exp  blo }", -1).uU();
	}

	[TestMethod]
	public void Recover4()
	{
		DoRecover3();
		var t = ser.Parse("{(a*b;");
		t = t/**/	.h("S").h("E", 1, 5, d: "par )", 1);
		t = t/**/				.h("E", 2, 5, d: '*').H(d: 'a').N(d: 'b').uuu();
		t = t/**/	.N("B", 0, 6, "sen exp  blo }", 1).u();
		t = t/**/.n(err: -1).H(null, 5, 5, "par )", -1).N(null, 6, 6, "sen exp  blo }", -1).uU();
		t = ser.Parse("({b;");
		t = t/**/	.h("S", 0, 4, "sen ;", 1).h("E", 0, 4, "plo )", 1);
		t = t/**/								.h("S", 2, 4).H("E", d: 'b').u();
		t = t/**/								.N("B", 1, 4, "sen exp  blo }", 1).uuu();
		t = t/**/.n(err: -1).H(null, 4, 4, "sen exp  blo }", -1).N(null, 4, 4, "plo )", -1);
		t = t/**/			.N(null, 4, 4, "sen ;", -1).uU();
		t = ser.Parse("{({b");
		t = t/**/	.h("S", 1, 4, "sen ;", 1).h("E", 1, 4, "plo )", 1);
		t = t/**/								.h("S", 3, 4, "sen ;", 1).H("E", 3, 4, 'b').u();
		t = t/**/								.N("B", 2, 4, "sen exp  blo }", 1).uu();
		t = t/**/	.N("B", 0, 4, "sen exp  blo }", 1).u();
		t = t/**/.n(err: -1).H(null, 4, 4, "sen ;", -1).N(null, 4, 4, "sen exp  blo }", -1);
		t = t/**/			.N(null, 4, 4, "plo )", -1).N(null, 4, 4, "sen ;", -1);
		t = t/**/			.N(null, 4, 4, "sen exp  blo }", -1).uU();
	}

	[TestMethod]
	public void Recover5()
	{
		DoRecover3();
		ser.Parse("+;").H("S", 0, 2, "sen blo  prog sen", 1).u();
		ser.Parse("*b;").H("S", 0, 3, "sen blo  prog sen", 1).u();
		var t = ser.Parse("a+;").Eq("Z");
		t = t/**/	.h("S", 0, 3, "add exp", 1).H("E", 0, 1, 'a').uu();
		t = t/**/.n(err: -1).H(null, 2, 3, "add exp", -1).uU();
		t = ser.Parse("*+;a+;");
		t = t/**/	.h("S", 0, 6, "add exp", 1).H("S", 0, 3, "sen blo  prog sen", 1);
		t = t/**/								.N("E", 3, 4, 'a').uu();
		t = t/**/.n(err: -1).H(null, 0, 1, "sen blo  prog sen", -1).N(null, 5, 6, "add exp", -1).uU();
		t = ser.Parse("a+b;a*;+b;");
		t = t/**/	.h("S", 0, 10, "sen exp", 1).h("S", 0, 7, "mul exp", 1);
		t = t/**/									.h("S", 0, 4).h("E", 0, 3, "a+b").u();
		t = t/**/									.N(d: 'a').uuu();
		t = t/**/.n(err: -1).H(null, 6, 7, "mul exp", -1).N(null, 7, 8, "sen exp", -1).uU();
		t = ser.Parse("a+b*a+");
		t = t/**/	.h("S", 0, 6, "add exp", 1).h("E", 0, 5, '+').H("E", 0, 1, 'a');
		t = t/**/											.n(d: '*').H(d: 'b').N(d: 'a').uuuu();
		t = t/**/.n(err: -1).H(null, 6, 6, "add exp", -1).uU();
	}

	[TestMethod]
	public void Recover6()
	{
		DoRecover3();
		var t = ser.Parse("()}");
		t = t/**/	.h("S", 0, 3, "sen ;", 1).H("E", 0, 2, "plo blo  par exp", 1).uu();
		t = t/**/.n(err: -1).H(null, 1, 2, "plo blo  par exp", -1).N(null, 2, 3, "sen ;", -1).uU();
		t = ser.Parse("b+();a;");
		t = t/**/	.h("S").h("S").h(d: '+').H(d: 'b').N("E", 2, 4, "plo blo  par exp", 1).uu();
		t = t/**/			.N("E", 5, 6, 'a').uu();
		t = t/**/.n(err: -1).H(null, 3, 4, "plo blo  par exp", -1).uU();
		t = ser.Parse("a;b+(a*);b{");
		t = t/**/	.h("S", 0, 11, "sen ;", 1);
		t = t/**/		.h("S").h("S").H(d: 'a').u();
		t = t/**/				.n(d: '+').H(d: 'b').n("E", 4, 8, "mul exp", 1).H(d: 'a').uuu();
		t = t/**/		.N("E", 9, 10, 'b').uu();
		t = t/**/.n(err: -1).H(null, 7, 8, "mul exp", -1).N(null, 10, 11, "sen ;", -1).uU();
		t = ser.Parse("a;b+(a*)b;a+;");
		t = t/**/	.h("S", 0, 13, "add exp", 1);
		t = t/**/		.h("S", 0, 10, "sen ;", 1).h("S").H(d: 'a').u();
		t = t/**/					.n(d: '+').H(d: 'b').n("E", 4, 8, "mul exp", 1).H(d: 'a').uuu();
		t = t/**/		.N("E", 10, 11, 'a').uu();
		t = t/**/.n(err: -1).H(null, 7, 8, "mul exp", -1).N(null, 8, 9, "sen ;", -1);
		t = t/**/			.N(null, 12, 13, "add exp", -1).uU();
	}
}
