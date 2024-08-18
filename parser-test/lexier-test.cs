//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.parser.meta;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1806:Do not ignore method results")]
[TestClass]
public class TestLexier : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	enum Lex { _, A, B, BB, C, CC, D };

	Lexier<Lex> ler;

	void NewLer(string grammar) => ler = new(MetaLex.Gram<Lex>(grammar, true), true) { errs = null };

	void Check(string read, string s) => Check(Encoding.UTF8.GetBytes(read), s);

	void Check(byte[] read, string s)
	{
		using var __ = ler.Begin(new LerByte(read));
		while (ler.Next()) ;
		var ls = string.Join(" ", ler.Lexs(0, ler.Loc()).Select(t => t.ToString()).ToArray());
		env.WriteLine(ls);
		AreEqual(s, ls);
	}

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
		NewLer("A=a \n B=b\n ==comm\n");
		Check("a", "A=a"); Check("b", "B=b"); Check("ab", "A=a B=b");
		Check("", ""); Check("c", "_!c");
	}

	[TestMethod]
	public void Lex12()
	{
		NewLer("A=a \n B=ab \n BB=bb \n C=cc");
		Check("a", "A=a"); Check("ab", "B=ab");
		Check("abbb", "B=ab BB=bb"); Check("ccbb", "C=cc BB=bb");
		Check("aabb", "A=a B=ab _!b"); Check("bcc", "_!b C=cc");
	}

	[TestMethod]
	public void Lex13()
	{
		NewLer("A=a \n B=abc \n BB=bc \n C=ccc \n D=cd");
		Check("abc", "B=abc"); Check("aabcccc", "A=a B=abc C=ccc");
		Check("bcb", "BB=bc _!b"); Check("ccd", "_!c D=cd");
	}

	[TestMethod]
	public void Lex23()
	{
		NewLer("A=ab \n B=abc \n C=cc \n CC=ccc");
		Check("ab", "A=ab"); Check("abc", "B=abc");
		Check("ababc", "A=ab B=abc"); Check("abdcc", "A=ab _!d C=cc");
		Check("abcc", "B=abc _!c"); Check("abccc", "B=abc C=cc");
		Check("ababcccc", "A=ab B=abc CC=ccc"); Check("a", "_!a");
	}

	[TestMethod]
	public void Lex24()
	{
		NewLer("A=ab \n B=abcd \n C=cccd");
		Check("a", "_!a"); Check("ab", "A=ab"); Check("abc", "A=ab _!c");
		Check("abcdab", "B=abcd A=ab"); Check("abcccd", "A=ab C=cccd");
	}

	[TestMethod]
	public void Lex123()
	{
		NewLer("A=a \n B=ab \n BB=ac \n C=abc");
		Check("a", "A=a"); Check("ab", "B=ab"); Check("ac", "BB=ac");
		Check("abc", "C=abc"); Check("aabcc", "A=a C=abc _!c");
	}

	[TestMethod]
	public void Lex1234()
	{
		NewLer("A=a \n B=ab \n C=abc \n CC=abd \n D=abcd");
		Check("abcd", "D=abcd"); Check("ababd", "B=ab CC=abd");
		Check("abd", "CC=abd"); Check("abcab", "C=abc B=ab");
	}

	[TestMethod]
	public void Lex16()
	{
		Throw(() => NewLer("A=0123456789abcdefg"), "15");
		Throw(() => NewLer("A=[abc^0-9A-Za-z]"), "No byte in A.1");
	}

	[TestMethod]
	public void Range1()
	{
		Throw(() => NewLer("A=a[^ab] \n B=ac"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Range2()
	{
		NewLer("A=[ab][0-2] \n B=[a-b][0-9^3-9]d");
		Check("a0", "A=a0"); Check("a0d", "B=a0d");
		Check("b2", "A=b2"); Check("b2d", "B=b2d");
	}

	[TestMethod]
	public void Range3()
	{
		NewLer("A=[^abAB]c \n B=[AB][]");
		Check("ac", "_!a _!c"); Check("bc", "_!b _!c");
		Check("cccc", "A=cc A=cc"); Check("\nc", "A=\nc"); Check("A~", "B=A~");
		Check("acc", "_!a A=cc");
	}

	[TestMethod]
	public void Range4()
	{
		NewLer("A=\\x_");
		Check("3_", "A=3_"); Check("b_", "A=b_");
		Check("G_", "_!G _!_"); Check("_", "_!_");
	}

	[TestMethod]
	public void Range5()
	{
		NewLer("A=[^a-z0-9]_");
		Check("A_", "A=A_"); Check(".._", "_!. A=._");
		Check("a_", "_!a _!_"); Check("2_", "_!2 _!_");
	}

	[TestMethod]
	public void Range6()
	{
		NewLer("A=[abc^b]_");
		Check("a_", "A=a_"); Check("c_", "A=c_"); Check("b_", "_!b _!_");
	}

	[TestMethod]
	public void Range7()
	{
		NewLer("A=[!-~^\\d\\a]");
		Check("!_^/+", "A=! A=_ A=^ A=/ A=+");
		Check(" 2Ab.", "_!  _!2 _!A _!b A=.");
	}

	[TestMethod]
	public void Range8()
	{
		Throw(() => NewLer("A=a[ac] \n B=a[bc]"), "Prefix.* of B.1 and A.1");
		Throw(() => NewLer("A=a[abc] \n B=a[bc]"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Alt1()
	{
		Throw(() => NewLer("A=ab| ac \n B=ab"), "B.1 and A.1 clash");
	}

	[TestMethod]
	public void Alt2()
	{
		NewLer("A=ab|ac");
		Check("ab", "A=ab"); Check("ac", "A=ac");
		Check("a", "_!a"); Check("ad", "_!a _!d"); Check("abc", "A=ab _!c");
	}

	[TestMethod]
	public void Alt3()
	{
		NewLer("A=abc|abd \n B=abcd \n C=acd");
		Check("abc", "A=abc"); Check("abd", "A=abd");
		Check("abcd", "B=abcd"); Check("acd", "C=acd");
	}

	[TestMethod]
	public void Alt4()
	{
		Throw(() => NewLer("A=a[a-c^ac]|a[b-b]"), "A.1 and A.1 clash");
		Throw(() => NewLer("A=a[a-c^a]|ab"), "Prefix.* of A.1 and A.1");
	}

	[TestMethod]
	public void Dup1()
	{
		NewLer("A=a+");
		Check("a", "A=a"); Check("aa", "A=aa"); Check("aaa", "A=aaa");
		Check("baa", "_!b A=aa"); Check("ab", "A=a _!b");
	}

	[TestMethod]
	public void Dup2()
	{
		NewLer("A=aa+");
		Check("a", "_!a"); Check("aa", "A=aa"); Check("aaa", "A=aaa");
		Check("baa", "_!b A=aa"); Check("ab", "_!a _!b");
	}

	[TestMethod]
	public void Dup3()
	{
		NewLer("A=ab+c");
		Check("abc", "A=abc"); Check("abbc", "A=abbc"); Check("abbbc", "A=abbbc");
		Check("ac", "_!a _!c"); Check("abb", "A!");
		Check("abbda", "A!d _!a"); Check("abbabc", "A!a _!b _!c");
	}

	[TestMethod]
	public void Dup4()
	{
		Throw(() => NewLer("A=a+a"), "A.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup5()
	{
		Throw(() => NewLer("A=a \n B=a+"), "B.1 and A.1 clash");
		Throw(() => NewLer("A=aa \n B=a+"), "B.1 and A.1 .*dup");
		Throw(() => NewLer("A=a \n B=a+b"), "B.1 and A.1 .*dup");
		Throw(() => NewLer("A=aa \n B=a+b"), "B.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup6()
	{
		Throw(() => NewLer("A=a+c \n B=aa"), "B.1 and A.1 .*dup");
		Throw(() => NewLer("A=a+b \n B=abc"), "B.1 and A.1 .*dup");
		Throw(() => NewLer("A=ab+c \n B=abcd"), "B.1 and A.1 .*dup");
		Throw(() => NewLer("A=abc \n B=a+b"), "B.1 and A.1 .*dup");
	}

	[TestMethod]
	public void Dup7()
	{
		NewLer("A=a+ \n B=a+b");
		Check("a", "A=a"); Check("ab", "B=ab");
		Check("aa", "A=aa"); Check("aab", "B=aab");
		Check("aaa", "A=aaa"); Check("aaab", "B=aaab");
	}

	[TestMethod]
	public void Dup8()
	{
		NewLer("A=a+b \n B=a+c");
		Check("abaac", "A=ab B=aac"); Check("b", "_!b"); Check("c", "_!c");
	}

	[TestMethod]
	public void Dup9()
	{
		Throw(() => NewLer("A=[ab]+ [bc]"), "A.2 and A.1 .*dup");
		Throw(() => NewLer("A=a[ab]+|b[bc]+ b"), "A.2 and A.1 .*dup");
	}

	[TestMethod]
	public void Wad1()
	{
		NewLer("A=aa b cde \n B=ab \n C=bc");
		Check("aabcdee", "A=aabcde _!e"); Check("abbc", "B=ab C=bc");
		Check("aa", "A!"); Check("aabcdf", "A!c _!d _!f");
		Check("aacab", "A!c B=ab");
	}

	[TestMethod]
	public void Wad2()
	{
		NewLer("A=a bcd|bce f \n B=abcf \n BB=bca \n C=c");
		Check("abcdf", "A=abcdf"); Check("abcef", "A=abcef");
		Check("abcf", "B=abcf"); Check("abcc", "A!b C=c C=c");
		Check("abca", "A!b C=c A!");
	}

	[TestMethod]
	public void Wad3()
	{
		NewLer("A=a b+|c d");
		Check("abd", "A=abd"); Check("abbd", "A=abbd");
		Check("ad", "A!d"); Check("abbc", "A!c"); Check("abbcd", "A!c _!d");
		Check("acd", "A=acd"); Check("acbd", "A!b _!d");
	}

	[TestMethod]
	public void Wad4()
	{
		NewLer("A=a b+e|c d");
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aecd", "A!e _!c _!d"); Check("abbccd", "A!c _!c _!d");
		Check("acd", "A=acd"); Check("acbd", "A!b _!d");
	}

	[TestMethod]
	public void Empty1()
	{
		NewLer("A=a |bc \n B=b \n D=d");
		Check("a", "A=a"); Check("abc", "A=abc");
		Check("ad", "A=a D=d"); Check("abcd", "A=abc D=d");
		Check("abd", "A=a B=b D=d");
	}

	[TestMethod]
	public void Empty2()
	{
		NewLer("A=a |bc d \n B=b \n D=d");
		Check("ad", "A=ad"); Check("abcd", "A=abcd");
		Check("a", "A!"); Check("abc", "A!");
		Check("abdd", "A!b D=d D=d");
	}

	[TestMethod]
	public void Empty3()
	{
		NewLer("A=a |bc b \n B=b \n D=d");
		Check("ab", "A=ab"); Check("abcb", "A=abcb");
		Check("a", "A!"); Check("abdd", "A=ab D=d D=d");
	}

	[TestMethod]
	public void Empty4()
	{
		NewLer("A=a |b+e d");
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aebed", "A!e _!b _!e _!d"); Check("abbcd", "A!c _!d");
		Check("ad", "A=ad");
	}

	[TestMethod]
	public void Loop1()
	{
		NewLer("A=a +c|b d");
		Check("abd", "A=abd"); Check("acbd", "A=acbd");
		Check("accd", "A!d"); Check("accbd", "A=accbd");
		Check("ad", "A!d");
	}

	[TestMethod]
	public void Loop2()
	{
		NewLer("A=a |+c|b+e d");
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("aebed", "A!e _!b _!e _!d"); Check("abbcd", "A!c _!d");
		Check("accd", "A=accd"); Check("acbed", "A=acbed");
		Check("ad", "A=ad");
	}

	[TestMethod]
	public void Loop3()
	{
		NewLer("A=a | +c|b+e");
		Check("abed", "A=abe _!d"); Check("abbe", "A=abbe");
		Check("aebe", "A=a _!e _!b _!e"); Check("abbcbe", "A!c _!b _!e");
		Check("acc", "A=acc"); Check("acbe", "A=acbe");
		Check("a", "A=a");
	}

	[TestMethod]
	public void Redo1()
	{
		NewLer("A=a *b c \n B=d \n C=cd");
		Check("abcd", "A=abc B=d"); Check("acbc", "A!c A=acbc");
		Check("acdd", "A!c A!d A!d A!"); Check("adebccd", "A!d A!e A=adebc C=cd");
	}

	[TestMethod]
	public void Redo2()
	{
		NewLer("A=a *bc d \n B=d \n C=cd");
		Check("abcdd", "A=abcd B=d"); Check("abdbcd", "A!b A!d A=abdbcd");
		Check("abedd", "A!b A!e A!d A!d A!"); Check("abebcdcd", "A!b A!e A=abebcd C=cd");
	}

	[TestMethod]
	public void Redo3()
	{
		NewLer("A=a *c|be d");
		Check("abed", "A=abed"); Check("abbed", "A!b A=abbed");
		Check("acd", "A=acd"); Check("acbed", "A!b _!e _!d");
		Check("abcd", "A!b A=abcd"); Check("abbcd", "A!b A!b A=abbcd");
		Check("aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void Redo4()
	{
		NewLer("A=a *c|b+e d");
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("acd", "A=acd"); Check("acbed", "A!b _!e _!d");
		Check("abbccd", "A!c A=abbccd"); Check("abbcbed", "A!c A=abbcbed");
		Check("aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void Redo5()
	{
		NewLer("A=a *+c|b+e d");
		Check("abed", "A=abed"); Check("abbed", "A=abbed");
		Check("acd", "A!d A!"); Check("acbed", "A=acbed");
		Check("abbccd", "A!c A!d A!"); Check("abbcbed", "A!c A=abbcbed");
		Check("aebed", "A!e A=aebed");
	}

	[TestMethod]
	public void Redo6()
	{
		Throw(() => new Lexier<Lex>(new LexGram<Lex>().k(Lex.A).redo["a"].w["b"]), "Redo");
		Throw(() => NewLer("A=*a b c"), "grammar");
		Throw(() => NewLer("A=a *|b c"), "grammar");
	}

	[TestMethod]
	public void Esc1()
	{
		NewLer(@"A=[\^\-\[\]\\\Z]");
		Check("^", "A=^"); Check("-", "A=-"); Check("[", "A=["); Check("]", "A=]");
		Check("\\", "A=\\"); Check("Z", "A=Z");
	}

	[TestMethod]
	public void Esc2()
	{
		NewLer(@"A=\[\]");
		Check("[]", "A=[]");
	}

	[TestMethod]
	public void Utf1()
	{
		Throw(() => NewLer("A=\\u+ \n B=\\u\\A"), "Prefix.* of B.1 and A.1");
		Throw(() => NewLer("A=\\u+ \n B=\\u\\u"), "B.1 and A.1 clash");
		Throw(() => NewLer("A=\\u+ \n B=\\A"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void Utf2()
	{
		NewLer("A=a\\u\\u\\uz|a\\u\\uz \n B=a");
		Check("a你za好z", "A=a你z A=a好z");
		Check("a\x80z", "A=a\x80z");
	}

	[TestMethod]
	public void Utf3()
	{
		NewLer("A=a[\\u\\A^z]+z \n B=[a\\u^\\u]");
		Check("a好za大家\t都好z", "A=a好z A=a大家\t都好z");
		Check("a", "B=a");
		var bs = Encoding.UTF8.GetBytes("a好z"); bs[2] = 0;
		Check(bs, "A=" + Encoding.UTF8.GetString(bs));
	}

	[TestMethod]
	public void SameKey1()
	{
		NewLer("A=a\nA=b");
		Check("a", "A=a"); Check("b", "A=b");
		NewLer("A=ab c\nA=abd");
		Check("abc", "A=abc"); Check("abd", "A=abd");
	}

	[TestMethod]
	public void SameKey2()
	{
		Throw(() => NewLer("A=a\nA=b|a"), "A.1 and A.1 clash");
	}
}
