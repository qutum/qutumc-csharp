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
using Ser = (SyntStr t, SynterStr s);

file static class Extension
{
	public static void Eq(this (bool empty, IEnumerable<char> first) eq, bool empty, string first)
	{
		AreEqual(empty, eq.empty);
		AreEqual(first, string.Join(" ", eq.first.Order()));
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
		make.First("X").Eq(true, "a c");
		make.First("Y").Eq(true, "c");
		make.First("Z").Eq(false, "a c d");
	}

	[TestMethod]
	public void FirstTigerT316()
	{
		NewMake(new Gram()
			.n("S")["E", '\0']
			.n("E")["T", "e"]
			.n("e")['+', "T", "e"]['-', "T", "e"][[]]
			.n("T")["F", "t"]
			.n("t")['*', "F", "t"]['/', "F", "t"][[]]
			.n("F")['a']['1']['(', "E", ')']);
		make.First("S").Eq(false, "( 1 a");
		make.First("E").Eq(false, "( 1 a");
		make.First("e").Eq(true, "+ -");
		make.First("T").Eq(false, "( 1 a");
		make.First("t").Eq(true, "* /");
		make.First("F").Eq(false, "( 1 a");
	}
}
