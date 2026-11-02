//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using static TestLexier.Lex;
using One = ByteSet.One;
using S = ByteSet;

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1806:Do not ignore method results")]
[TestClass]
public class TestLexier : IDisposable
{
	readonly EnvWriter env = EnvWriter.Use();

	public void Dispose() => env.Dispose();

	public enum Lex { _, A, B, BB, C, CC, D };

	Lexier<Lex> ler;

	static LexGram<Lex> Gram => new();

	void Ler(LexGram<Lex> gram) => ler = new(gram, true) { errs = null };

	void Check(string read, string s) => Check(Encoding.UTF8.GetBytes(read), s);

	void Check(byte[] read, string s)
	{
		using var __ = ler.Begin(new LerByte(read));
		while (ler.Next()) ;
		var ls = ler.Dumper();
		env.WriteLine(ls);
		AreEqual(s, ls);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0058:Do not use asserts in catch blocks")]
	static void Throw(Action a, string reg)
	{
		try {
			a();
		}
		catch (Exception e) {
			if (new Regex(reg).IsMatch(e.Message))
				return;
			Fail($"Expected Regex <{reg}> Actual <{e.Message}>");
		}
		Fail($"Expected Regex <{reg}> Actually No Excpetion");
	}

	[TestMethod]
	public void Lex1()
	{
		Ler(Gram.k(A).w["a"].k(B).w["b"]);
		Check("a", "A=a"); Check("b", "B=b"); Check("ab", "A=a B=b");
		Check("", ""); Check("c", "_!c");
	}

	[TestMethod]
	public void Lex12()
	{
		Ler(Gram.k(A).w["a"].k(B).w["a", "b"].k(BB).w["bb"].k(C).w["cc"]);
		Check("a", "A=a"); Check("ab", "B=ab");
		Check("abbb", "B=ab BB=bb"); Check("ccbb", "C=cc BB=bb");
		Check("aabb", "A=a B=ab _!b"); Check("bcc", "_!b C=cc");
	}

	[TestMethod]
	public void Lex13()
	{
		Ler(Gram.k(A).w["a"].k(B).w["abc"].k(BB).w["bc"].k(C).w["ccc"].k(D).w["cd"]);
		Check("abc", "B=abc"); Check("aabcccc", "A=a B=abc C=ccc");
		Check("bcb", "BB=bc _!b"); Check("ccd", "_!c D=cd");
	}

	[TestMethod]
	public void Lex23()
	{
		Ler(Gram.k(A).w["ab"].k(B).w["abc"].k(C).w["cc"].k(CC).w["ccc"]);
		Check("ab", "A=ab"); Check("abc", "B=abc");
		Check("ababc", "A=ab B=abc"); Check("abdcc", "A=ab _!d C=cc");
		Check("abcc", "B=abc _!c"); Check("abccc", "B=abc C=cc");
		Check("ababcccc", "A=ab B=abc CC=ccc"); Check("a", "_!a");
	}

	[TestMethod]
	public void Lex24()
	{
		Ler(Gram.k(A).w["ab"].k(B).w["abcd"].k(C).w["cccd"]);
		Check("a", "_!a"); Check("ab", "A=ab"); Check("abc", "A=ab _!c");
		Check("abcdab", "B=abcd A=ab"); Check("abcccd", "A=ab C=cccd");
	}

	[TestMethod]
	public void Lex123()
	{
		Ler(Gram.k(A).w["a"].k(B).w["ab"].k(BB).w["ac"].k(C).w["abc"]);
		Check("a", "A=a"); Check("ab", "B=ab"); Check("ac", "BB=ac");
		Check("abc", "C=abc"); Check("aabcc", "A=a C=abc _!c");
	}

	[TestMethod]
	public void Lex1234()
	{
		Ler(Gram.k(A).w["a"].k(B).w["ab"].k(C).w["abc"].k(CC).w["abd"].k(D).w["abcd"]);
		Check("abcd", "D=abcd"); Check("ababd", "B=ab CC=abd");
		Check("abd", "CC=abd"); Check("abcab", "C=abc B=ab");
	}

	[TestMethod]
	public void Lex16()
	{
		Throw(() => Ler(Gram.k(A).w["0123456789abcdefg"]), "15");
		Throw(() => Ler(Gram.k(A).w[""]), "No byte in A.1.1");
		Throw(() => Ler(Gram.k(A).w[(One)""]), "No byte in A.1.1");
	}

	[TestMethod]
	public void Set1()
	{
		Throw(() => Ler(Gram.k(A).w["a", S.All - "ab"].k(B).w["ac"]), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Set2()
	{
		Ler(Gram.k(A).w[(One)"ab", (One)"012"].k(B).w[(One)"ab", (One)"012", "d"]);
		Check("a0", "A=a0"); Check("a0d", "B=a0d");
		Check("b2", "A=b2"); Check("b2d", "B=b2d");
	}

	[TestMethod]
	public void Set3()
	{
		Ler(Gram.k(A).w[S.Lines - "abAB", "c"].k(B).w[(One)"AB", S.Lines]);
		Check("ac", "_!a _!c"); Check("bc", "_!b _!c");
		Check("cccc", "A=cc A=cc"); Check("\nc", "A=\nc"); Check("A~", "B=A~");
		Check("acc", "_!a A=cc");
	}

	[TestMethod]
	public void Set4()
	{
		Ler(Gram.k(A).w[S.Hex, "_"]);
		Check("3_", "A=3_"); Check("b_", "A=b_");
		Check("G_", "_!G _!_"); Check("_", "_!_");
	}

	[TestMethod]
	public void Set5()
	{
		Ler(Gram.k(A).w[S.Lines - S.Dec - "abcdefghijklmnopqrstuvwxyz", "_"]);
		Check("A_", "A=A_"); Check(".._", "_!. A=._");
		Check("a_", "_!a _!_"); Check("2_", "_!2 _!_");
	}

	[TestMethod]
	public void Set6()
	{
		Ler(Gram.k(A).w[(One)"abc" - "b", "_"]);
		Check("a_", "A=a_"); Check("c_", "A=c_"); Check("b_", "_!b _!_");
	}

	[TestMethod]
	public void Set7()
	{
		Ler(Gram.k(A).w[S.Op + "_" - S.Dec - S.Alpha]);
		Check("!_^/+", "A=! A=_ A=^ A=/ A=+");
		Check(" 2Ab.", "_!  _!2 _!A _!b A=.");
	}

	[TestMethod]
	public void Set8()
	{
		Throw(() => Ler(Gram.k(A).w["a", (One)"ac"].k(B).w["a", (One)"bc"]), "Prefix.* of B.1 and A.1");
		Throw(() => Ler(Gram.k(A).w["a", (One)"abc"].k(B).w["a", (One)"bc"]), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Alt1()
	{
		Throw(() => Ler(Gram.k(A).w["ab"]["ac"].k(B).w["ab"]), "B.1 and A.1 clash");
	}

	[TestMethod]
	public void Alt2()
	{
		Ler(Gram.k(A).w["ab"]["ac"]);
		Check("ab", "A=ab"); Check("ac", "A=ac");
		Check("a", "_!a"); Check("ad", "_!a _!d"); Check("abc", "A=ab _!c");
	}

	[TestMethod]
	public void Alt3()
	{
		Ler(Gram.k(A).w["abc"]["abd"].k(B).w["abcd"].k(C).w["acd"]);
		Check("abc", "A=abc"); Check("abd", "A=abd");
		Check("abcd", "B=abcd"); Check("acd", "C=acd");
	}

	[TestMethod]
	public void Alt4()
	{
		Throw(() => Ler(Gram.k(A).w["a", (One)"b"]["a", (One)"b"]), "A.1 and A.1 clash");
		Throw(() => Ler(Gram.k(A).w["a", (One)"bc"]["ab"]), "Prefix.* of A.1 and A.1");
	}

	[TestMethod]
	public void Dup1()
	{
		Ler(Gram.k(A).w["a", ..]);
		Check("a", "A=a"); Check("aa", "A=aa"); Check("aaa", "A=aaa");
		Check("baa", "_!b A=aa"); Check("ab", "A=a _!b");
	}

	[TestMethod]
	public void Dup2()
	{
		Ler(Gram.k(A).w["a", "a", ..]);
		Check("a", "_!a"); Check("aa", "A=aa"); Check("aaa", "A=aaa");
		Check("baa", "_!b A=aa"); Check("ab", "_!a _!b");
	}

	[TestMethod]
	public void Dup3()
	{
		Ler(Gram.k(A).w["a", "b", .., "c"]);
		Check("abc", "A=abc"); Check("abbc", "A=abbc"); Check("abbbc", "A=abbbc");
		Check("ac", "_!a _!c"); Check("abb", "A!");
		Check("abbda", "A!d _!a"); Check("abbabc", "A!a _!b _!c");
	}

	[TestMethod]
	public void Dup4()
	{
		Throw(() => Ler(Gram.k(A).w["a", .., "a"]), "A.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup5()
	{
		Throw(() => Ler(Gram.k(A).w["a"].k(B).w["a", ..]), "B.1 and A.1 clash");
		Throw(() => Ler(Gram.k(A).w["aa"].k(B).w["a", ..]), "B.1 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a"].k(B).w["a", .., "b"]), "B.1 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["aa"].k(B).w["a", .., "b"]), "B.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup6()
	{
		Throw(() => Ler(Gram.k(A).w["a", .., "c"].k(B).w["a", "a"]), "B.1 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a", .., "b"].k(B).w["a", "b", "c"]), "B.1 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a", "b", .., "c"].k(B).w["a", "b", "c", "d"]), "B.1 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a", "b", "c"].k(B).w["a", .., "b"]), "B.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup7()
	{
		Ler(Gram.k(A).w["a", ..].k(B).w["a", .., "b"]);
		Check("a", "A=a"); Check("ab", "B=ab");
		Check("aa", "A=aa"); Check("aab", "B=aab");
		Check("aaa", "A=aaa"); Check("aaab", "B=aaab");
	}

	[TestMethod]
	public void Dup8()
	{
		Ler(Gram.k(A).w["a", .., "b"].k(B).w["a", .., "c"]);
		Check("abaac", "A=ab B=aac"); Check("b", "_!b"); Check("c", "_!c");
	}

	[TestMethod]
	public void Dup9()
	{
		Throw(() => Ler(Gram.k(A).w[(One)"ab", ..].w[(One)"bc"]), "A.2 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a", (One)"ab", ..]["b", (One)"bc", ..].w["b"]), "A.2 and A.1 .*dup");
		Throw(() => Ler(Gram.k(A).w["a", ..].w[[]]["b"].w["a"]), "A.3 and A.1 .*dup");
	}

	[TestMethod]
	public void Wad1()
	{
		Ler(Gram.k(B).w["ab"].k(A).w["aa"].w["b"].w["cde"].k(C).w["bc"]);
		Check("aabcdee", "A=aabcde _!e"); Check("abbc", "B=ab C=bc");
		Check("aa", "A!"); Check("aabcdf", "A!c _!d _!f");
		Check("aacab", "A!c B=ab");
	}

	[TestMethod]
	public void Wad2()
	{
		Ler(Gram.k(A).w["a"].w["bcd"]["bce"].w["f"].k(B).w["abcf"].k(BB).w["bca"].k(C).w["c"]);
		Check("abcdf", "A=abcdf"); Check("abcef", "A=abcef");
		Check("abcf", "B=abcf"); Check("abcc", "A!b C=c C=c");
		Check("abca", "A!b C=c A!");
	}

	[TestMethod]
	public void Wad3()
	{
		Ler(Gram.k(A).w["a"].w["b", ..]["c"].w["d"]);
		Check("abd", "A=abd"); Check("abbd", "A=abbd");
		Check("ad", "A!d"); Check("abbc", "A!c"); Check("abbcd", "A!c _!d");
		Check("acd", "A=acd"); Check("acbd", "A!b _!d");
	}

	[TestMethod]
	public void Wad4()
	{
		Ler(Gram.k(A).w["a"].w["b", .., "e"]["c"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aecd", "A!e _!c _!d"); Check("abbccd", "A!c _!c _!d");
		Check("acd", "A=acd"); Check("acbd", "A!b _!d");
	}

	[TestMethod]
	public void Empty1()
	{
		Ler(Gram.k(A).w["a"].w[[]]["bc"].k(B).w["b"].k(D).w["d"]);
		Check("a", "A=a"); Check("abc", "A=abc");
		Check("ad", "A=a D=d"); Check("abcd", "A=abc D=d");
		Check("abd", "A=a B=b D=d");
	}

	[TestMethod]
	public void Empty2()
	{
		Ler(Gram.k(A).w["a"].w[[]]["bc"].w["d"].k(B).w["b"].k(D).w["d"]);
		Check("ad", "A=ad"); Check("abcd", "A=abcd");
		Check("a", "A!"); Check("abc", "A!");
		Check("abdd", "A!b D=d D=d");
	}

	[TestMethod]
	public void Empty3()
	{
		Ler(Gram.k(A).w["a"].w[[]]["bc"].w["b"].k(B).w["b"].k(D).w["d"]);
		Check("ab", "A=ab"); Check("abcb", "A=abcb");
		Check("a", "A!"); Check("abdd", "A=ab D=d D=d");
	}

	[TestMethod]
	public void Empty4()
	{
		Ler(Gram.k(A).w["a"].w[[]]["b", .., "e"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aebed", "A!e _!b _!e _!d"); Check("abbcd", "A!c _!d");
		Check("ad", "A=ad");
	}

	[TestMethod]
	public void Loop1()
	{
		Ler(Gram.k(A).w["a"].w["c"].loop["b"].w["d"]);
		Check("abd", "A=abd"); Check("acbd", "A=acbd");
		Check("accd", "A!d"); Check("accbd", "A=accbd");
		Check("ad", "A!d");
	}

	[TestMethod]
	public void Loop2()
	{
		Ler(Gram.k(A).w["a"].w[[]]["c"].loop["b", .., "e"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aebed", "A!e _!b _!e _!d"); Check("abbcd", "A!c _!d");
		Check("accd", "A=accd"); Check("acbed", "A=acbed");
		Check("ad", "A=ad");
	}

	[TestMethod]
	public void Loop3()
	{
		Ler(Gram.k(A).w["a"].w[[]]["c"].loop["b", .., "e"]);
		Check("abed", "A=abe _!d"); Check("abbe", "A=abbe");
		Check("aebe", "A=a _!e _!b _!e"); Check("abbcbe", "A!c _!b _!e");
		Check("acc", "A=acc"); Check("acbe", "A=acbe");
		Check("a", "A=a");
	}

	[TestMethod]
	public void Redo1()
	{
		Ler(Gram.k(A).w["a"].redo["b"].w["c"].k(B).w["d"].k(C).w["cd"]);
		Check("abcd", "A=abc B=d"); Check("acbc", "A!c A=acbc");
		Check("acdd", "A!c A!d A!d A!"); Check("adebccd", "A!d A!e A=adebc C=cd");
	}

	[TestMethod]
	public void Redo2()
	{
		Ler(Gram.k(A).w["a"].redo["bc"].w["d"].k(B).w["d"].k(C).w["cd"]);
		Check("abcdd", "A=abcd B=d"); Check("abdbcd", "A!b A!d A=abdbcd");
		Check("abedd", "A!b A!e A!d A!d A!"); Check("abebcdcd", "A!b A!e A=abebcd C=cd");
	}

	[TestMethod]
	public void Redo3()
	{
		Ler(Gram.k(A).w["a"].redo["c"]["be"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A!b A=abbed");
		Check("acd", "A=acd"); Check("acbed", "A!b _!e _!d");
		Check("abcd", "A!b A=abcd"); Check("abbcd", "A!b A!b A=abbcd");
		Check("aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void Redo4()
	{
		Ler(Gram.k(A).w["a"].redo["c"]["b", .., "e"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("acd", "A=acd"); Check("acbed", "A!b _!e _!d");
		Check("abbccd", "A!c A=abbccd"); Check("abbcbed", "A!c A=abbcbed");
		Check("aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void Redo5()
	{
		Ler(Gram.k(A).w["a"].redo["c"].loop["b", .., "e"].w["d"]);
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("acd", "A!d A!"); Check("acbed", "A=acbed");
		Check("abbccd", "A!c A!d A!"); Check("abbcbed", "A!c A=abbcbed");
		Check("aebed", "A!e A=aebed");
	}

	[TestMethod]
	public void Redo6()
	{
		Throw(() => Ler(Gram.k(A).redo["a"].w["b"]), "Redo first");
		Throw(() => Ler(Gram.k(A).w["a"].redo[[]]["b"].w["c"]), "Redo empty");
	}

	[TestMethod]
	public void Utf1()
	{
		Throw(() => Ler(Gram.k(A).w["\x80", ..].k(B).w["\x80", (One)"\x80"]), "B.1 and A.1 clash");
		Throw(() => Ler(Gram.k(A).w["\x80", ..].k(B).w["\x80", S.All]), "Prefix.* of B.1 and A.1");
		Throw(() => Ler(Gram.k(A).w["\x80", ..].k(B).w[S.All]), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Utf2()
	{
		Ler(Gram.k(A).w["a\x80\x80\x80z"]["a\x80\x80z"].k(B).w["a"]);
		Check("a你za好z", "A=a你z A=a好z");
		Check("a\x80z", "A=a\x80z");
	}

	[TestMethod]
	public void Utf3()
	{
		Ler(Gram.k(A).w["a", S.All - "z", .., "z"].k(B).w[(One)"a\x80" - "\x80"]);
		Check("a好za大家\t都好z", "A=a好z A=a大家\t都好z");
		Check("a", "B=a");
		var bs = Encoding.UTF8.GetBytes("a好z"); bs[2] = 0;
		Check(bs, "A=" + Encoding.UTF8.GetString(bs));
	}

	[TestMethod]
	public void SameKey1()
	{
		Ler(Gram.k(A).w["a"].k(A).w["b"]);
		Check("a", "A=a"); Check("b", "A=b");
		Ler(Gram.k(A).w["ab"].w["c"].k(A).w["abd"].k(B).w["ac"]);
		Check("abc", "A=abc"); Check("abd", "A=abd"); Check("ac", "B=ac");
	}

	[TestMethod]
	public void SameKey2()
	{
		Throw(() => Ler(Gram.k(A).w["a"].k(A).w["b"]["a"]), "A.1 and A.1 clash");
	}
}
