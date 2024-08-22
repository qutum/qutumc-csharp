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

using ClashEq = (HashSet<char>, short[] redus, short[] shifts)[];
using Gram = SynGram<char, string>;

file static class Extension
{
	public static void Eq(this (bool empty, IEnumerable<char> first) test, bool empty, string first)
	{
		AreEqual(empty, test.empty);
		AreEqual(first, string.Join(" ", test.first.Order()));
	}

	public static void Eq(this Dictionary<SerMaker<char, string>.Clash, (HashSet<char> ks, short m)> tests,
		params ClashEq aims)
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
		AreNotEqual(null, a);
		ser = new TestSynter { ser = new(n => n[0], a, f, r) { recover = recover } };
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
		NewMer(new Gram().n("S")["E"]
			.n("E")["E", ",", "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
			.n(",")[','].clash
			.n("E")['a']['b']
		);
		mer.Firsts(); mer.Forms();
		ClashEq eq = [
			([','], [1], [~4]),
			([','], [2], [~4]),
			([','], [3], [~4]),
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['+'], [~3], [2]),
			(['^'], [1], [~3]),
			(['^'], [2], [~3]),
			(['^'], [3], [~3])
		];
		mer.Clashs().Eq(eq);
	}

	[TestMethod]
	public void Clash4()
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
	public void Clash5()
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
		ser.Parse("").H(null, 0, 0, "Z S  S a", -1).uU();
		ser.Parse("c").H(null, 0, 1, "Z S  S a", -1).uU();
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
		ser.Parse("-a").H(null, 0, 1, "sentence expression  expression a", -1).uU();
		ser.Parse("a+").H(null, 2, 2, "addition expression  expression a", -1).uU();
		ser.Parse("a++a").H(null, 2, 3, "addition expression  expression a", -1).uU();
		ser.Parse("a+a--a").H(null, 4, 5, "subtraction expression  expression a", -1).uU();
		ser.Parse(")").H(null, 0, 1, "sentence expression  expression a", -1).uU();
		ser.Parse("a)").H(null, 1, 2, "sentence ;", -1).uU();
		ser.Parse("(a))").H(null, 3, 4, "sentence ;", -1).uU();
		ser.Parse("(").H(null, 1, 1, "parenth expression  expression a", -1).uU();
		ser.Parse("(a").H(null, 2, 2, "parenth )", -1).uU();
		ser.Parse("(a;").H(null, 2, 3, "parenth )", -1).uU();
		ser.Parse(";a").H(null, 0, 1, "sentence expression  expression a", -1).uU();
		ser.Parse("a;;").H(null, 2, 3, "sentence expression  expression a", -1).uU();
		ser.Parse("(a;a)").H(null, 2, 3, "parenth )", -1).uU();
	}

	[TestMethod]
	public void Recover1()
	{
		NewMer(new Gram().n("Z")["S"]
			.n("S", "sentence")["E", ';'].recover["S", "E", ';'].recover.labelLow
			.n("E", "expression")['a', ..]['b', ..]
		);
		NewSer(recover: true);
		ser.Parse("a;").h("S").H("E", d: 'a').uuU();
		ser.Parse("a;b;").h("S").h("S").H("E", d: 'a').u().N("E", d: 'b').uuU();
		ser.Parse("").H("S", 0, 0, "sentence expression  sentence", 1).u().n(err: -1).U();
		ser.Parse(";").H("S", err: 1).u().n(err: -1).H(err: -1).uU();
		var t = ser.Parse(";;").Eq("Z");
		t = t/**/	.h("S", 0, 2, d: "sentence expression", 1);
		t = t/**/		.H("S", 0, 1, "sentence expression  sentence", 1).uu();
		t = t/**/.n(err: -1).H(null, 0, 1, "sentence expression  sentence", -1);
		t = t/**/			.N(null, 1, 2, "sentence expression", -1).uU();
	}
}
