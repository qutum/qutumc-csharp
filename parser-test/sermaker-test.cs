//
// Qutum 10 Compiler
// Copyright 2008-2025 Qianyan Cai
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

[TestClass]
public class TestSerMaker : IDisposable
{
	readonly EnvWriter env = EnvWriter.Use();

	public void Dispose() => env.Dispose();

	SerMaker<char, string> mer;
	TestSynter ser;

	public void NewMer(Gram gram)
	{
		mer = new(gram, k => k, n => n[0]);
	}

	public static void Eq((bool empty, IEnumerable<char> first) test, bool empty, string first)
	{
		AreEqual(empty, test.empty);
		AreEqual(first, string.Join(" ", test.first.Order()));
	}

	public void ClashEq(params (HashSet<char>, short[] redus, short[] shifts)[] aims)
	{
		var tests = mer.Clashs();
		AreEqual(tests.Count, aims.Length);
		var bz = tests.First().Key.redus.bits.Length;
		foreach (var (keys, redus, shifts) in aims) {
			IsTrue(tests.TryGetValue(new() {
				redus = new BitSet { bits = new ulong[bz] }.Or(redus.Select(a => a ^ a >> 15)),
				shifts = shifts == null ? default
					: new BitSet { bits = new ulong[bz] }.Or(shifts.Select(a => a ^ a >> 15))
			}, out var t));
			IsTrue(keys.SetEquals(mer.KeySet(t.keys)));
			AreEqual(shifts?.Min() < 0 ? 0 : redus.Min() < 0 ? SynForm.Redu(~redus.Min()) : -1,
				int.Min(t.go, 0));
		}
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
		Eq(mer.First("X"), true, "a c");
		Eq(mer.First("Y"), true, "c");
		Eq(mer.First("Z"), false, "a c d");
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
		Eq(mer.First("S"), false, "( 1 a");
		Eq(mer.First("E"), false, "( 1 a");
		Eq(mer.First("e"), true, "+ -");
		Eq(mer.First("T"), false, "( 1 a");
		Eq(mer.First("t"), true, "* /");
		Eq(mer.First("F"), false, "( 1 a");
	}

	[TestMethod]
	public void Clash1()
	{
		NewMer(new Gram().n("S")["E"].n("E")['i']["E", "E"]['j']);
		mer.Firsts(); mer.Forms();
		ClashEq(
			(['i'], [2], [1]),
			(['j'], [2], [3])
		);
		NewMer(new Gram().n("S")["E"].n("E")['i']["E", "E"]['j'].clash);
		mer.Firsts(); mer.Forms();
		ClashEq(
			(['i'], [2], [1]),
			(['j'], [2], [3])
		);
		NewMer(new Gram().n("S")["E"].n("E")['i'].clash["E", "E"].clash['j'].clash);
		mer.Firsts(); mer.Forms();
		ClashEq(
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
		ClashEq(
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
		ClashEq(
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
		ClashEq(
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
		ClashEq(
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
		ClashEq( // no way
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
		ClashEq(
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
		ClashEq(
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
		ClashEq(
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
		ClashEq( // no way
			([','], [1], [2]),
			(['+'], [1], [3]),
			(['^'], [1], [4])
		);
	}

	[TestMethod]
	public void Clash8()
	{
		NewMer(new Gram().n("S")["B"]
			.n("B")["b", .., "B"].syntRight[[]]
			.n("b")["a", "J"]
			.n("j")['(', .., "b", "B", ')'].synt
			.n("J")["j", "J"][[]]
			.n("a")['1', ..].synt['2', ..].synt['3', ..].synt['4', ..].synt['5', ..].synt
		);
		mer.Firsts(); mer.Forms();
		AreEqual(null, mer.Clashs());
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
		ClashEq((['|'], [4], [~5]));
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
		ClashEq(
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
		_ = ser.Parse("ab").Eq("A", 'a').Leaf.N("B", 'b').U;
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
		var _ = ser.Parse("a+1").Eq("S");
		_ = _/**/	.H("W", 'a');
		_ = _/**/	.n("E", '+').H("N", '1').uuU;
		_ = ser.Parse("a+1-b");
		_ = _/**/	.H("W", 'a');
		_ = _/**/	.n("E", '+').H("N", '1').u;
		_ = _/**/	.n("E", '-').H("W", 'b').uuU;
		_ = ser.Parse("2*b-b+1/a");
		_ = _/**/	.H("N", '2');
		_ = _/**/	.n("E", '*').H("W", 'b').u;
		_ = _/**/	.n("E", '-').H("W", 'b').u;
		_ = _/**/	.n("E", '+').H("N", '1').n("E", '/').H("W", 'a').uuuU;
		_ = ser.Parse("1+#a#1#b*2");
		_ = _/**/.H("N", '1').n("E", '+');
		_ = _/**/					.h("E", '#').H("W", 'a').u;
		_ = _/**/					.n("E", '#').H("N", '1').u;
		_ = _/**/					.n("E", '#').H("W", 'b').u;
		_ = _/**/					.n("E", '*').H("N", '2').uuuU;
	}

	[TestMethod]
	public void ErrorCyclic()
	{
		NewMer(new Gram()
			.n("Z")["S", 'a'].clash.syntOmit
			.n("S")['a', ..]["S"].clash
		);
		NewSer();
		_ = ser.Parse("aa").Eq(err: -1).h(null, "cyclic grammar", (0, 1), -2).uU;
	}

	[TestMethod]
	public void Error1()
	{
		NewMer(new Gram().n("S")['a']);
		NewSer();
		_ = ser.Parse("").H(null, "S a", (0, 0), -1).uU;
		_ = ser.Parse("b").H(null, "S a", (0, 1), -1).uU;
		NewMer(new Gram().n("Z")["S"].n("S")['a']['b']);
		NewSer();
		_ = ser.Parse("").H(null, "Z S", (0, 0), -1).uU;
		_ = ser.Parse("c").H(null, "Z S", (0, 1), -1).uU;
		_ = ser.Parse("aa").H(null, "end of", (1, 2), -1).uU;
	}

	[TestMethod]
	public void Error2()
	{
		NewMer(new Gram().n("P")["S"].syntOmit
			.n("S", "sentence")
					["E"]["S", ';', "E"]
			.n("E", "expression", false)
					["A"]["B"]
					['(', .., "E", ')'].label("parenth")
					['a']
			.n("A")["E", '+', .., "E"].clash.label("addition")
			.n("B")["E", '-', .., "E"].clash.label("subtraction")
		);
		NewSer();
		_ = ser.Parse("aa").H(null, "sentence ;", (1, 2), -1).uU;
		_ = ser.Parse("-a").H(null, "sentence expression  sentence sentence", (0, 1), -1).uU;
		_ = ser.Parse("a+").H(null, "addition expression", (2, 2), -1).uU;
		_ = ser.Parse("a++a").H(null, "addition expression", (2, 3), -1).uU;
		_ = ser.Parse("a+a--a").H(null, "subtraction expression", (4, 5), -1).uU;
		_ = ser.Parse(")").H(null, "sentence expression  sentence sentence", (0, 1), -1).uU;
		_ = ser.Parse("a)").H(null, "sentence ;", (1, 2), -1).uU;
		_ = ser.Parse("(a))").H(null, "sentence ;", (3, 4), -1).uU;
		_ = ser.Parse("(").H(null, "parenth expression", (1, 1), -1).uU;
		_ = ser.Parse("(a").H(null, "parenth )", (2, 2), -1).uU;
		_ = ser.Parse("(a;").H(null, "parenth )", (2, 3), -1).uU;
		_ = ser.Parse(";a").H(null, "sentence expression  sentence sentence", (0, 1), -1).uU;
		_ = ser.Parse("a;;").H(null, "sentence expression", (2, 3), -1).uU;
		_ = ser.Parse("(a;a)").H(null, "parenth )", (2, 3), -1).uU;
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
		var _ = ser.Parse("").H(null, "sen exp", (0, 0), -1).uU;
		_ = ser.Parse(";").H("S", "sen exp", (0, 1), 1).u;
		_ = ser.Parse("aa").Eq("Z");
		_ = _/**/	.h("S", "sen ;", (0, 2), 1).H("E", 'a', (0, 1)).uu;
		_ = _/**/.n(err: -1).H(null, "sen ;", (1, 2), -1).uU;
		_ = ser.Parse("a;;b;;").Eq("Z");
		_ = _/**/	.H("S", "5:6! end of", (0, 6), 1).u;
		_ = _/**/.n(err: -1).H(null, "sen sen", (2, 3), -1);
		_ = _/**/			.N(null, "end of", (3, 4), -1).N(null, "end of", (5, 6), -1).uU;
		_ = ser.Parse(";;b;;").Eq("Z");
		_ = _/**/	.H("S", "4:5! end of", (0, 5), 1).u;
		_ = _/**/.n(err: -1).H(null, "sen exp", (0, 1), -1).N(null, "end of", (1, 2), -1);
		_ = _/**/			.N(null, "end of", (2, 3), -1).N(null, "end of", (4, 5), -1).uU;
	}

	[TestMethod]
	public void Recover2()
	{
		NewMer(new Gram().n("Z")["S"]
			.n("S", "sen")["E", ';'].recover["S", "E", ';'].recover
			.n("E", "exp")['a', ..]['b', ..]
		);
		NewSer(true);
		var _ = ser.Parse("a;").h("S").H("E", 'a').uuU;
		_ = ser.Parse("a;b;").h("S").h("S").H("E", 'a').u.N("E", 'b').uuU;
		_ = ser.Parse("").H(null, "sen exp  sen sen", (0, 0), -1).uU;
		_ = ser.Parse(";").H("S", "sen exp  sen sen", (0, 1), 1).u;
		_ = ser.Parse("aa").Eq("Z");
		_ = _/**/	.h("S", "sen ;", (0, 2), 1).H("E", 'a', (0, 1)).uu;
		_ = _/**/.n(err: -1).H(null, "sen ;", (1, 2), -1).uU;
		_ = ser.Parse("a;b").Eq("Z");
		_ = _/**/	.h("S", "sen ;", (0, 3), 1).h("S", j: (0, 2)).H("E", 'a', (0, 1)).u;
		_ = _/**/								.N("E", 'b', (2, 3)).uu;
		_ = _/**/.n(err: -1).H(null, "sen ;", (3, 3), -1).uU;
		_ = ser.Parse(";;a;;").Eq("Z");
		_ = _/**/	.h("S", "4:5! sen exp", (0, 5), 1);
		_ = _/**/		.h("S", j: (0, 4)).h("S", "1:2! sen exp", (0, 2), 1);
		_ = _/**/						.H("S", "0:1! sen exp  sen sen", (0, 1), 1).u;
		_ = _/**/					.N("E", 'a', (2, 3)).uuu;
		_ = _/**/.n(err: -1).H(null, "sen exp  sen sen", (0, 1), -1);
		_ = _/**/			.N(null, "sen exp", (1, 2), -1);
		_ = _/**/			.N(null, "sen exp", (4, 5), -1).uU;
	}

	void DoRecover3()
	{
		NewMer(new Gram().n("Z")["S"]
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
		var _ = ser.Parse("a+b){;b;");
		_ = _/**/	.h("S").h("S", "sen ;", (0, 6), 1);
		_ = _/**/				.h("E", '+', (0, 3)).H(d: 'a').N(d: 'b').uu;
		_ = _/**/			.N("E", 'b', (6, 7)).uu;
		_ = _/**/.n(err: -1).H(null, "sen ;", (3, 4), -1).uU;
		_ = ser.Parse("(a*b;");
		_ = _/**/	.h("S").h("E", "par )", (0, 4), 1);
		_ = _/**/				.h("E", '*', (1, 4)).H(d: 'a').N(d: 'b').uuuu;
		_ = _/**/.n(err: -1).H(null, "par )", (4, 4), -1).uU;
		_ = ser.Parse("{a*b;");
		_ = _/**/	.h("S", j: (1, 5)).h("E", '*', j: (1, 4)).H(d: 'a').N(d: 'b').uu;
		_ = _/**/	.N("B", "blo }  sen exp", (0, 5), 1).u;
		_ = _/**/.n(err: -1).H(null, "blo }  sen exp", (5, 5), -1).uU;
	}

	[TestMethod]
	public void Recover4()
	{
		DoRecover3();
		var _ = ser.Parse("{(a*b;");
		_ = _/**/	.h("S").h("E", "par )", (1, 5), 1);
		_ = _/**/				.h("E", '*', (2, 5)).H(d: 'a').N(d: 'b').uuu;
		_ = _/**/	.N("B", "blo }  sen exp", (0, 6), 1).u;
		_ = _/**/.n(err: -1).H(null, "par )", (5, 5), -1).N(null, "blo }  sen exp", (6, 6), -1).uU;
		_ = ser.Parse("({b;");
		_ = _/**/	.h("S", "sen ;", (0, 4), 1).h("E", "plo )", (0, 4), 1);
		_ = _/**/								.h("S", j: (2, 4)).H("E", 'b').u;
		_ = _/**/								.N("B", "blo }  sen exp", (1, 4), 1).uuu;
		_ = _/**/.n(err: -1).H(null, "blo }  sen exp", (4, 4), -1).N(null, "plo )", (4, 4), -1);
		_ = _/**/			.N(null, "sen ;", (4, 4), -1).uU;
		_ = ser.Parse("{({b");
		_ = _/**/	.h("S", "sen ;", (1, 4), 1).h("E", "plo )", (1, 4), 1);
		_ = _/**/								.h("S", "sen ;", (3, 4), 1).H("E", 'b', (3, 4)).u;
		_ = _/**/								.N("B", "blo }  sen exp", (2, 4), 1).uu;
		_ = _/**/	.N("B", "blo }  sen exp", (0, 4), 1).u;
		_ = _/**/.n(err: -1).H(null, "sen ;", (4, 4), -1).N(null, "blo }  sen exp", (4, 4), -1);
		_ = _/**/			.N(null, "plo )", (4, 4), -1).N(null, "sen ;", (4, 4), -1);
		_ = _/**/			.N(null, "blo }  sen exp", (4, 4), -1).uU;
	}

	[TestMethod]
	public void Recover5()
	{
		var z = SerMaker<char, string>.ErrZ; SerMaker<char, string>.ErrZ = 2;
		DoRecover3();
		SerMaker<char, string>.ErrZ = z;
		var _ = ser.Parse("+;").H("S", "sen blo  sen exp ...", (0, 2), 1).u;
		_ = ser.Parse("*b;").H("S", "sen blo  sen exp ...", (0, 3), 1).u;
		_ = ser.Parse("a+;").Eq("Z");
		_ = _/**/	.h("S", "add exp", (0, 3), 1).H("E", 'a', (0, 1)).uu;
		_ = _/**/.n(err: -1).H(null, "add exp", (2, 3), -1).uU;
		_ = ser.Parse("*+;a+;");
		_ = _/**/	.h("S", "add exp", (0, 6), 1).H("S", "sen blo  sen exp ...", (0, 3), 1);
		_ = _/**/								.N("E", 'a', (3, 4)).uu;
		_ = _/**/.n(err: -1).H(null, "sen blo  sen exp ...", (0, 1), -1).N(null, "add exp", (5, 6), -1).uU;
		_ = ser.Parse("a+b;a*;+b;");
		_ = _/**/	.h("S", "sen exp", (0, 10), 1).h("S", "mul exp", (0, 7), 1);
		_ = _/**/									.h("S", j: (0, 4)).h("E", "a+b", (0, 3)).u;
		_ = _/**/									.N(d: 'a').uuu;
		_ = _/**/.n(err: -1).H(null, "mul exp", (6, 7), -1).N(null, "sen exp", (7, 8), -1).uU;
		_ = ser.Parse("a+b*a+");
		_ = _/**/	.h("S", "add exp", (0, 6), 1).h("E", '+', (0, 5)).H("E", 'a', (0, 1));
		_ = _/**/											.n(d: '*').H(d: 'b').N(d: 'a').uuuu;
		_ = _/**/.n(err: -1).H(null, "add exp", (6, 6), -1).uU;
	}

	[TestMethod]
	public void Recover6()
	{
		DoRecover3();
		var _ = ser.Parse("()}");
		_ = _/**/	.h("S", "sen ;", (0, 3), 1).H("E", "plo blo  par exp", (0, 2), 1).uu;
		_ = _/**/.n(err: -1).H(null, "plo blo  par exp", (1, 2), -1).N(null, "sen ;", (2, 3), -1).uU;
		_ = ser.Parse("b+();a;");
		_ = _/**/	.h("S").h("S").h(d: '+').H(d: 'b').N("E", "plo blo  par exp", (2, 4), 1).uu;
		_ = _/**/			.N("E", 'a', (5, 6)).uu;
		_ = _/**/.n(err: -1).H(null, "plo blo  par exp", (3, 4), -1).uU;
		_ = ser.Parse("a;b+(a*);b{");
		_ = _/**/	.h("S", "sen ;", (0, 11), 1);
		_ = _/**/		.h("S").h("S").H(d: 'a').u;
		_ = _/**/				.n(d: '+').H(d: 'b').n("E", "mul exp", (4, 8), 1).H(d: 'a').uuu;
		_ = _/**/		.N("E", 'b', (9, 10)).uu;
		_ = _/**/.n(err: -1).H(null, "mul exp", (7, 8), -1).N(null, "sen ;", (10, 11), -1).uU;
		_ = ser.Parse("a;b+(a*)b;a+;");
		_ = _/**/	.h("S", "add exp", (0, 13), 1);
		_ = _/**/		.h("S", "sen ;", (0, 10), 1).h("S").H(d: 'a').u;
		_ = _/**/					.n(d: '+').H(d: 'b').n("E", "mul exp", (4, 8), 1).H(d: 'a').uuu;
		_ = _/**/		.N("E", 'a', (10, 11)).uu;
		_ = _/**/.n(err: -1).H(null, "mul exp", (7, 8), -1).N(null, "sen ;", (8, 9), -1);
		_ = _/**/			.N(null, "add exp", (12, 13), -1).uU;
	}
}
