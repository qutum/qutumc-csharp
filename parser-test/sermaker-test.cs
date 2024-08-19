//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Gram = SynGram<char, string>;
using ClashEq = (HashSet<char>, short[] redus, short[] shifts)[];

file static class Extension
{
	public static void Eq(this (bool empty, IEnumerable<char> first) eq, bool empty, string first)
	{
		AreEqual(empty, eq.empty);
		AreEqual(first, string.Join(" ", eq.first.Order()));
	}

	public static void Eq(this Dictionary<SerMaker<char, string>.Clash, (HashSet<char> ks, short m)> actus,
		params ClashEq eqs)
	{
		AreEqual(actus.Count, eqs.Length);
		foreach (var (ks, redus, shifts) in eqs) {
			IsTrue(actus.TryGetValue(new() {
				redus = redus.Select(a => (ushort)(a ^ a >> 15)).ToHashSet(),
				shifts = shifts?.Select(a => (ushort)(a ^ a >> 15)).ToHashSet()
			}, out var eq));
			IsTrue(eq.ks.SetEquals(ks));
			AreEqual(Math.Min((int)eq.m, 0),
				shifts?.Min() < 0 ? 0 : redus.Min() < 0 ? SynForm.Reduce(~redus.Min()) : -1);
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

	public void NewSer()
	{
		var (a, f, r) = mer.Make(out var _);
		AreNotEqual(null, a);
		ser = new TestSynter { ser = new(n => n[0], a, f, r) };
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
			(['\0'], [7, ~9], null),
			(['\0'], [7, 9, 10, ~12], null)
		);
	}

	[TestMethod]
	public void Make1()
	{
		NewMer(new Gram().n("S")[[]]);
		NewSer();
		ser.Parse("").Eq("S"); ser.Parse("a").Eq(err: -1);
		NewMer(new Gram()
			.n("Z")["S", 'a'].clash.syntOmit
			.n("S")['a', ..]["S"].clash
		);
		NewSer();
		ser.Parse("a").Eq("S", 0, 1, "loop grammar", err: -1);
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
	public void Error1()
	{
		NewMer(new Gram().n("S")['a']);
		NewSer();
		ser.Parse("").Eq(null, 0, 0, "S a", -1);
		ser.Parse("b").Eq(null, 0, 1, "S a", -1);
		NewMer(new Gram().n("Z")["S"].n("S")['a']['b']);
		NewSer();
		ser.Parse("").Eq(null, 0, 0, "Z S  S a", -1);
		ser.Parse("c").Eq(null, 0, 1, "Z S  S a", -1);
		ser.Parse("aa").Eq(null, 1, 2, "end of", -1);
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
		ser.Parse("aa").Eq(null, 1, 2, "sentence ;", -1);
		ser.Parse("-a").Eq(null, 0, 1, "sentence expression  expression a", -1);
		ser.Parse("a+").Eq(null, 2, 2, "addition expression  expression a", -1);
		ser.Parse("a++a").Eq(null, 2, 3, "addition expression  expression a", -1);
		ser.Parse("a+a--a").Eq(null, 4, 5, "subtraction expression  expression a", -1);
		ser.Parse(")").Eq(null, 0, 1, "sentence expression  expression a", -1);
		ser.Parse("a)").Eq(null, 1, 2, "sentence ;", -1);
		ser.Parse("(a))").Eq(null, 3, 4, "sentence ;", -1);
		ser.Parse("(").Eq(null, 1, 1, "parenth expression  expression a", -1);
		ser.Parse("(a").Eq(null, 2, 2, "parenth )", -1);
		ser.Parse("(a;").Eq(null, 2, 3, "parenth )", -1);
		ser.Parse(";a").Eq(null, 0, 1, "sentence expression  expression a", -1);
		ser.Parse("a;;").Eq(null, 2, 3, "sentence expression  expression a", -1);
		ser.Parse("(a;a)").Eq(null, 2, 3, "parenth )", -1);
	}
}
