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
using ClashEq = (HashSet<char>, short[] redus, short[] shifts)[];

file static class Extension
{
	public static void Eq(this (bool empty, IEnumerable<char> first) eq, bool empty, string first)
	{
		AreEqual(empty, eq.empty);
		AreEqual(first, string.Join(" ", eq.first.Order()));
	}

	public static void Eq(this Dictionary<SerMaker<char, string>.Clash, (HashSet<char> ks, short m)> eqs,
		params ClashEq s)
	{
		AreEqual(eqs.Count, s.Length);
		foreach (var (ks, redus, shifts) in s) {
			IsTrue(eqs.TryGetValue(new() {
				redus = redus.Select(a => (short)(a ^ a >> 15)).ToHashSet(),
				shifts = shifts?.Select(a => (short)(a ^ a >> 15)).ToHashSet()
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

	public void NewMake(Gram gram)
	{
		mer = new(gram, k => k, n => n[0], (_) => { });
	}

	[TestMethod]
	public void FirstTigerG312()
	{
		NewMake(new Gram().n("Z")['d']["X", "Y", "Z"].n("Y")[[]]['c'].n("X")["Y"]['a']);
		mer.Firsts();
		mer.First("X").Eq(true, "a c");
		mer.First("Y").Eq(true, "c");
		mer.First("Z").Eq(false, "a c d");
	}

	[TestMethod]
	public void FirstTigerT316()
	{
		NewMake(new Gram()
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
		NewMake(new Gram().n("E")['i']["E", "E"]['j']);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['i'], [1], [0]),
			(['j'], [1], [2])
		);
		mer.alts[2].clash = 1;
		mer.Clashs().Eq(
			(['i'], [1], [0]),
			(['j'], [1], [2])
		);
		mer.alts[0].clash = mer.alts[1].clash = 1;
		mer.Clashs().Eq(
			(['i'], [~1], [0]),
			(['j'], [1], [~2])
		);
	}

	[TestMethod]
	public void Clash2()
	{
		NewMake(new Gram()
			.n("E")["E", "E"].clash
					["E", '+', "E"].clash
					['x'].clash
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['+'], [0], [~1]),
			(['+'], [~1], [1]),
			(['x'], [0], [~2]),
			(['x'], [1], [~2])
		);
		NewMake(new Gram()
			.n("E")['x'].clash
					["E", "E"].clash
					["E", '+', "E"].clash
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['+'], [1], [~2]),
			(['+'], [~2], [2]),
			(['x'], [~1], [0]),
			(['x'], [~2], [0])
		);
	}

	[TestMethod]
	public void Clash3()
	{
		NewMake(new Gram()
			.n("E")["E", ',', "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
					['a']['b']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			([','], [~0], [0]),
			([','], [~1], [0]),
			([','], [~2], [0]),
			(['+'], [0], [~1]),
			(['+'], [~1], [1]),
			(['+'], [~2], [1]),
			(['^'], [0], [~2]),
			(['^'], [1], [~2]),
			(['^'], [2], [~2])
		);
		NewMake(new Gram()
			.n("E")["E", ",", "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
			.n(",")[','].clash
			.n("E")['a']['b']
		);
		mer.Firsts(); mer.Forms();
		ClashEq eq = [
			([','], [0], [~3]),
			([','], [1], [~3]),
			([','], [2], [~3]),
			(['+'], [0], [~1]),
			(['+'], [~1], [1]),
			(['+'], [~2], [1]),
			(['^'], [0], [~2]),
			(['^'], [1], [~2]),
			(['^'], [2], [~2])
		];
		mer.Clashs().Eq(eq);
		mer.alts[3].clash = 2;
		mer.Clashs().Eq(eq);
	}

	[TestMethod]
	public void Clash4()
	{
		NewMake(new Gram()
			.n("E")["E", '?', "E", ':', "E", '?', "E"].clashRight
					["E", '+', "E"].clash
					["E", '$', "E"].clash
					["E", '$'].clash
					['a']
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq(
			(['?', ':'], [0], [~0]),
			(['?'], [~1], [0]),
			(['?'], [~2], [0]),
			(['+'], [0], [~1]),
			(['+'], [~1], [1]),
			(['+'], [~2], [1]),
			(['$'], [0], [2, ~3]),
			(['$'], [1], [2, ~3]),
			(['$'], [2], [2, ~3])
		);
	}

	[TestMethod]
	public void ClashTigerF332()
	{
		NewMake(new Gram()
			.n("P")["L"]
			.n("S")['i', '=', 'i']
					['*', 'i', ':', "S"]
					['(', "S", ')']
					['?', 'i', '&', "S"]
					['?', 'i', '&', "S", '|', "S"]
			.n("L")["S"]["L", ';', "S"]
		);
		mer.Firsts(); mer.Forms();
		mer.Clashs().Eq((['|'], [4], [5]));
		mer.alts[4].clash = mer.alts[5].clash = 1;
		mer.Clashs().Eq((['|'], [4], [~5]));
	}

	[TestMethod]
	public void ClashTigerF337()
	{
		NewMake(new Gram()
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
			(['|'], [~3], [3]),
			(['|'], [~4], [3]),
			(['&'], [3], [~4]),
			(['&'], [~4], [4]),
			(['^'], [7], [~7]),
			(['\0'], [6, ~8], null),
			(['\0'], [6, 8, 9, ~11], null)
		);
	}
}
