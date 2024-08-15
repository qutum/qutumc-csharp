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
using ClashEq = (HashSet<char>, short[], short?)[];

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
		foreach (var (ks, redus, shift) in s) {
			int mode = shift < 0 ? 0 : redus.Min() < 0 ? SynForm.Reduce(~redus.Min()) : -1;
			IsTrue(eqs.TryGetValue(new() {
				shift = (short?)(shift ^ shift >> 15),
				redus = redus.Select(r => (short)(r ^ r >> 15)).ToHashSet()
			}, out var eq));
			IsTrue(eq.ks.SetEquals(ks));
			AreEqual(Math.Min((int)eq.m, 0), mode);
		}
	}
}

[TestClass]
public class TestSerMaker : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	SerMaker<char, string> make;

	public void NewMake(Gram gram)
	{
		make = new(gram, k => k, n => n[0], (_) => { });
	}

	[TestMethod]
	public void FirstTigerG312()
	{
		NewMake(new Gram().n("Z")['d']["X", "Y", "Z"].n("Y")[[]]['c'].n("X")["Y"]['a']);
		make.Firsts();
		make.First("X").Eq(true, "a c");
		make.First("Y").Eq(true, "c");
		make.First("Z").Eq(false, "a c d");
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
		make.Firsts();
		make.First("S").Eq(false, "( 1 a");
		make.First("E").Eq(false, "( 1 a");
		make.First("e").Eq(true, "+ -");
		make.First("T").Eq(false, "( 1 a");
		make.First("t").Eq(true, "* /");
		make.First("F").Eq(false, "( 1 a");
	}

	[TestMethod]
	public void Clash1()
	{
		NewMake(new Gram().n("E")['i']["E", "E"]['1']);
		make.Firsts(); make.Forms();
		make.Clashs().Eq(
			(['i'], [1], 0),
			(['1'], [1], 2)
		);
		make.alts[2].clash = 1;
		make.Clashs().Eq(
			(['i'], [1], 0),
			(['1'], [1], 2)
		);
		make.alts[0].clash = make.alts[1].clash = 1;
		make.Clashs().Eq(
			(['i'], [~1], 0),
			(['1'], [1], ~2)
		);
	}

	[TestMethod]
	public void Clash2()
	{
		NewMake(new Gram()
			.n("E")["E", ',', "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
					['a']['b']
		);
		make.Firsts(); make.Forms();
		make.Clashs().Eq(
			([','], [~0], 0),
			([','], [~1], 0),
			([','], [~2], 0),
			(['+'], [0], ~1),
			(['+'], [~1], 1),
			(['+'], [~2], 1),
			(['^'], [0], ~2),
			(['^'], [1], ~2),
			(['^'], [2], ~2)
		);
		NewMake(new Gram()
			.n("E")["E", ",", "E"].clash
					["E", '+', "E"].clash
					["E", '^', "E"].clashRight
			.n(",")[','].clash
			.n("E")['a']['b']
		);
		make.Firsts(); make.Forms();
		ClashEq eq = [
			([','], [0], ~3),
			([','], [1], ~3),
			([','], [2], ~3),
			(['+'], [0], ~1),
			(['+'], [~1], 1),
			(['+'], [~2], 1),
			(['^'], [0], ~2),
			(['^'], [1], ~2),
			(['^'], [2], ~2)
		];
		make.Clashs().Eq(eq);
		make.alts[3].clash = 2;
		make.Clashs().Eq(eq);
	}

	[TestMethod]
	public void Clash3()
	{
		NewMake(new Gram()
			.n("E")["E", '?', "E", ':', "E", '?', "E"].clashRight
					["E", '+', "E"].clash
					['a']['b']
		);
		make.Firsts(); make.Forms();
		make.Clashs().Eq(
			(['?', ':'], [0], ~0),
			(['?'], [~1], 0),
			(['+'], [0], ~1),
			(['+'], [~1], 1)
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
		make.Firsts(); make.Forms();
		make.Clashs().Eq((['|'], [4], 5));
		make.alts[4].clash = make.alts[5].clash = 1;
		make.Clashs().Eq((['|'], [4], ~5));
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
		make.Firsts(); make.Forms();
		make.Clashs().Eq(
			(['|'], [~3], 3),
			(['|'], [~4], 3),
			(['&'], [3], ~4),
			(['&'], [~4], 4),
			(['^'], [7], ~7),
			(['\0'], [6, ~8], null),
			(['\0'], [6, 8, 9, ~11], null)
		);
	}
}
