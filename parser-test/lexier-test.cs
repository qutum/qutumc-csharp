//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
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

	public void Dispose() { GC.SuppressFinalize(this); env.Dispose(); }

	enum Tag { _, A, B, BB, C, CC, D };

	class Ler : Lexier<Tag>
	{
		public Ler(LexGram<Tag> grammar) : base(grammar, true) { errs = null; }
		public Ler(string grammar) : base(MetaLex.Gram<Tag>(grammar, true), true) { }
	}

	void Check(Ler l, string input, string s) => Check(l, Encoding.UTF8.GetBytes(input), s);

	void Check(Ler l, byte[] input, string s)
	{
		using var __ = l.Begin(new LerByte(input));
		while (l.Next()) ;
		var z = string.Join(" ", l.Lexs(0, l.Loc()).Select(t => t.ToString()).ToArray());
		env.WriteLine(z);
		AreEqual(s, z);
	}

	static void Throw(Action a, string reg)
	{
		try {
			a();
		}
		catch (Exception e) {
			if (new Regex(reg).IsMatch(e.Message))
				return;
			Fail($"Expected Regex: {reg}, Actual: {e.Message}");
		}
		Fail($"Expected Regex: {reg}, Actually No Excpetion");
	}

	[TestMethod]
	public void Lex1()
	{
		var l = new Ler("A=a \n B=b\n ==comm\n");
		Check(l, "a", "A=a"); Check(l, "b", "B=b"); Check(l, "ab", "A=a B=b");
		Check(l, "", ""); Check(l, "c", "_!c");
	}

	[TestMethod]
	public void Lex12()
	{
		var l = new Ler("A=a \n B=ab \n BB=bb \n C=cc");
		Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
		Check(l, "abbb", "B=ab BB=bb"); Check(l, "ccbb", "C=cc BB=bb");
		Check(l, "aabb", "A=a B=ab _!b"); Check(l, "bcc", "_!b C=cc");
	}

	[TestMethod]
	public void Lex13()
	{
		var l = new Ler("A=a \n B=abc \n BB=bc \n C=ccc \n D=cd");
		Check(l, "abc", "B=abc"); Check(l, "aabcccc", "A=a B=abc C=ccc");
		Check(l, "bcb", "BB=bc _!b"); Check(l, "ccd", "_!c D=cd");
	}

	[TestMethod]
	public void Lex23()
	{
		var l = new Ler("A=ab \n B=abc \n C=cc \n CC=ccc");
		Check(l, "ab", "A=ab"); Check(l, "abc", "B=abc");
		Check(l, "ababc", "A=ab B=abc"); Check(l, "abdcc", "A=ab _!d C=cc");
		Check(l, "abcc", "B=abc _!c"); Check(l, "abccc", "B=abc C=cc");
		Check(l, "ababcccc", "A=ab B=abc CC=ccc"); Check(l, "a", "_!a");
	}

	[TestMethod]
	public void Lex24()
	{
		var l = new Ler("A=ab \n B=abcd \n C=cccd");
		Check(l, "a", "_!a"); Check(l, "ab", "A=ab"); Check(l, "abc", "A=ab _!c");
		Check(l, "abcdab", "B=abcd A=ab"); Check(l, "abcccd", "A=ab C=cccd");
	}

	[TestMethod]
	public void Lex123()
	{
		var l = new Ler("A=a \n B=ab \n BB=ac \n C=abc");
		Check(l, "a", "A=a"); Check(l, "ab", "B=ab"); Check(l, "ac", "BB=ac");
		Check(l, "abc", "C=abc"); Check(l, "aabcc", "A=a C=abc _!c");
	}

	[TestMethod]
	public void Lex1234()
	{
		var l = new Ler("A=a \n B=ab \n C=abc \n CC=abd \n D=abcd");
		Check(l, "abcd", "D=abcd"); Check(l, "ababd", "B=ab CC=abd");
		Check(l, "abd", "CC=abd"); Check(l, "abcab", "C=abc B=ab");
	}

	[TestMethod]
	public void Lex16()
	{
		Throw(() => new Ler("A=0123456789abcdefg"), "15");
		Throw(() => new Ler("A=[abc^0-9A-Za-z]"), "No byte in A.1");
	}

	[TestMethod]
	public void LexRange1()
	{
		Throw(() => new Ler("A=a[^ab] \n B=ac"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void LexRange2()
	{
		var l = new Ler("A=[ab][0-2] \n B=[a-b][0-9^3-9]d");
		Check(l, "a0", "A=a0"); Check(l, "a0d", "B=a0d");
		Check(l, "b2", "A=b2"); Check(l, "b2d", "B=b2d");
	}

	[TestMethod]
	public void LexRange3()
	{
		var l = new Ler("A=[^abAB]c \n B=[AB][]");
		Check(l, "ac", "_!a _!c"); Check(l, "bc", "_!b _!c");
		Check(l, "cccc", "A=cc A=cc"); Check(l, "\nc", "A=\nc"); Check(l, "A~", "B=A~");
		Check(l, "acc", "_!a A=cc");
	}

	[TestMethod]
	public void LexRange4()
	{
		var l = new Ler("A=\\x_");
		Check(l, "3_", "A=3_"); Check(l, "b_", "A=b_");
		Check(l, "G_", "_!G _!_"); Check(l, "_", "_!_");
	}

	[TestMethod]
	public void LexRange5()
	{
		var l = new Ler("A=[^a-z0-9]_");
		Check(l, "A_", "A=A_"); Check(l, ".._", "_!. A=._");
		Check(l, "a_", "_!a _!_"); Check(l, "2_", "_!2 _!_");
	}

	[TestMethod]
	public void LexRange6()
	{
		var l = new Ler("A=[abc^b]_");
		Check(l, "a_", "A=a_"); Check(l, "c_", "A=c_"); Check(l, "b_", "_!b _!_");
	}

	[TestMethod]
	public void LexRange7()
	{
		var l = new Ler("A=[!-~^\\d\\a]");
		Check(l, "!_^/+", "A=! A=_ A=^ A=/ A=+");
		Check(l, " 2Ab.", "_!  _!2 _!A _!b A=.");
	}

	[TestMethod]
	public void LexRange8()
	{
		Throw(() => new Ler("A=a[ac] \n B=a[bc]"), "Prefix.* of B.1 and A.1");
		Throw(() => new Ler("A=a[abc] \n B=a[bc]"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void LexAlt1()
	{
		Throw(() => new Ler("A=ab| ac \n B=ab"), "B.1 and A.1 conflict");
	}

	[TestMethod]
	public void LexAlt2()
	{
		var l = new Ler("A=ab|ac");
		Check(l, "ab", "A=ab"); Check(l, "ac", "A=ac");
		Check(l, "a", "_!a"); Check(l, "ad", "_!a _!d"); Check(l, "abc", "A=ab _!c");
	}

	[TestMethod]
	public void LexAlt3()
	{
		var l = new Ler("A=abc|abd \n B=abcd \n C=acd");
		Check(l, "abc", "A=abc"); Check(l, "abd", "A=abd");
		Check(l, "abcd", "B=abcd"); Check(l, "acd", "C=acd");
	}

	[TestMethod]
	public void LexAlt4()
	{
		Throw(() => new Ler("A=a[a-c^ac]|a[b-b]"), "A.1 and A.1 conflict");
		Throw(() => new Ler("A=a[a-c^a]|ab"), "Prefix.* of A.1 and A.1");
	}

	[TestMethod]
	public void LexRepeat1()
	{
		var l = new Ler("A=a+");
		Check(l, "a", "A=a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
		Check(l, "baa", "_!b A=aa"); Check(l, "ab", "A=a _!b");
	}

	[TestMethod]
	public void LexRepeat2()
	{
		var l = new Ler("A=aa+");
		Check(l, "a", "_!a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
		Check(l, "baa", "_!b A=aa"); Check(l, "ab", "_!a _!b");
	}

	[TestMethod]
	public void LexRepeat3()
	{
		var l = new Ler("A=ab+c");
		Check(l, "abc", "A=abc"); Check(l, "abbc", "A=abbc"); Check(l, "abbbc", "A=abbbc");
		Check(l, "ac", "_!a _!c"); Check(l, "abb", "A!");
		Check(l, "abbda", "A!d _!a"); Check(l, "abbabc", "A!a _!b _!c");
	}

	[TestMethod]
	public void LexRepeat4()
	{
		Throw(() => new Ler("A=a+a"), "A.1 and A.1 .*repeat");
	}

	[TestMethod]
	public void LexRepeat5()
	{
		Throw(() => new Ler("A=a \n B=a+"), "B.1 and A.1 conflict");
		Throw(() => new Ler("A=aa \n B=a+"), "B.1 and A.1 .*repeat");
		Throw(() => new Ler("A=a \n B=a+b"), "B.1 and A.1 .*repeat");
		Throw(() => new Ler("A=aa \n B=a+b"), "B.1 and A.1 .*repeat");
	}

	[TestMethod]
	public void LexRepeat6()
	{
		Throw(() => new Ler("A=a+c \n B=aa"), "B.1 and A.1 .*repeat");
		Throw(() => new Ler("A=a+b \n B=abc"), "B.1 and A.1 .*repeat");
		Throw(() => new Ler("A=ab+c \n B=abcd"), "B.1 and A.1 .*repeat");
		Throw(() => new Ler("A=abc \n B=a+b"), "B.1 and A.1 .*repeat");
	}

	[TestMethod]
	public void LexRepeat7()
	{
		var l = new Ler("A=a+ \n B=a+b");
		Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
		Check(l, "aa", "A=aa"); Check(l, "aab", "B=aab");
		Check(l, "aaa", "A=aaa"); Check(l, "aaab", "B=aaab");
	}

	[TestMethod]
	public void LexRepeat8()
	{
		var l = new Ler("A=a+b \n B=a+c");
		Check(l, "abaac", "A=ab B=aac"); Check(l, "b", "_!b"); Check(l, "c", "_!c");
	}

	[TestMethod]
	public void LexRepeat9()
	{
		Throw(() => new Ler("A=[ab]+ [bc]"), "A.2 and A.1 .*repeat");
		Throw(() => new Ler("A=a[ab]+|b[bc]+ b"), "A.2 and A.1 .*repeat");
	}

	[TestMethod]
	public void LexPart1()
	{
		var l = new Ler("A=aa b cde \n B=ab \n C=bc");
		Check(l, "aabcdee", "A=aabcde _!e"); Check(l, "abbc", "B=ab C=bc");
		Check(l, "aa", "A!"); Check(l, "aabcdf", "A!c _!d _!f");
		Check(l, "aacab", "A!c B=ab");
	}

	[TestMethod]
	public void LexPart2()
	{
		var l = new Ler("A=a bcd|bce f \n B=abcf \n BB=bca \n C=c");
		Check(l, "abcdf", "A=abcdf"); Check(l, "abcef", "A=abcef");
		Check(l, "abcf", "B=abcf"); Check(l, "abcc", "A!b C=c C=c");
		Check(l, "abca", "A!b C=c A!");
	}

	[TestMethod]
	public void LexPart3()
	{
		var l = new Ler("A=a b+|c d");
		Check(l, "abd", "A=abd"); Check(l, "abbd", "A=abbd");
		Check(l, "ad", "A!d"); Check(l, "abbc", "A!c"); Check(l, "abbcd", "A!c _!d");
		Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b _!d");
	}

	[TestMethod]
	public void LexPart4()
	{
		var l = new Ler("A=a b+e|c d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
		Check(l, "aecd", "A!e _!c _!d"); Check(l, "abbccd", "A!c _!c _!d");
		Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b _!d");
	}

	[TestMethod]
	public void LexEmpty1()
	{
		var l = new Ler("A=a |bc \n B=b \n D=d");
		Check(l, "a", "A=a"); Check(l, "abc", "A=abc");
		Check(l, "ad", "A=a D=d"); Check(l, "abcd", "A=abc D=d");
		Check(l, "abd", "A=a B=b D=d");
	}

	[TestMethod]
	public void LexEmpty2()
	{
		var l = new Ler("A=a |bc d \n B=b \n D=d");
		Check(l, "ad", "A=ad"); Check(l, "abcd", "A=abcd");
		Check(l, "a", "A!"); Check(l, "abc", "A!");
		Check(l, "abdd", "A!b D=d D=d");
	}

	[TestMethod]
	public void LexEmpty3()
	{
		var l = new Ler("A=a |bc b \n B=b \n D=d");
		Check(l, "ab", "A=ab"); Check(l, "abcb", "A=abcb");
		Check(l, "a", "A!"); Check(l, "abdd", "A=ab D=d D=d");
	}

	[TestMethod]
	public void LexEmpty4()
	{
		var l = new Ler("A=a |b+e d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
		Check(l, "aebed", "A!e _!b _!e _!d"); Check(l, "abbcd", "A!c _!d");
		Check(l, "ad", "A=ad");
	}

	[TestMethod]
	public void LexLoop1()
	{
		var l = new Ler("A=a +c|b d");
		Check(l, "abd", "A=abd"); Check(l, "acbd", "A=acbd");
		Check(l, "accd", "A!d"); Check(l, "accbd", "A=accbd");
		Check(l, "ad", "A!d");
	}

	[TestMethod]
	public void LexLoop2()
	{
		var l = new Ler("A=a |+c|b+e d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
		Check(l, "aebed", "A!e _!b _!e _!d"); Check(l, "abbcd", "A!c _!d");
		Check(l, "accd", "A=accd"); Check(l, "acbed", "A=acbed");
		Check(l, "ad", "A=ad");
	}

	[TestMethod]
	public void LexLoop3()
	{
		var l = new Ler("A=a | +c|b+e");
		Check(l, "abed", "A=abe _!d"); Check(l, "abbe", "A=abbe");
		Check(l, "aebe", "A=a _!e _!b _!e"); Check(l, "abbcbe", "A!c _!b _!e");
		Check(l, "acc", "A=acc"); Check(l, "acbe", "A=acbe");
		Check(l, "a", "A=a");
	}

	[TestMethod]
	public void LexSkip1()
	{
		var l = new Ler("A=a *b c \n B=d \n C=cd");
		Check(l, "abcd", "A=abc B=d"); Check(l, "acbc", "A!c A=acbc");
		Check(l, "acdd", "A!c A!d A!d A!"); Check(l, "adebccd", "A!d A!e A=adebc C=cd");
	}

	[TestMethod]
	public void LexSkip2()
	{
		var l = new Ler("A=a *bc d \n B=d \n C=cd");
		Check(l, "abcdd", "A=abcd B=d"); Check(l, "abdbcd", "A!b A!d A=abdbcd");
		Check(l, "abedd", "A!b A!e A!d A!d A!"); Check(l, "abebcdcd", "A!b A!e A=abebcd C=cd");
	}

	[TestMethod]
	public void LexSkip3()
	{
		var l = new Ler("A=a *c|be d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A!b A=abbed");
		Check(l, "acd", "A=acd"); Check(l, "acbed", "A!b _!e _!d");
		Check(l, "abcd", "A!b A=abcd"); Check(l, "abbcd", "A!b A!b A=abbcd");
		Check(l, "aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void LexSkip4()
	{
		var l = new Ler("A=a *c|b+e d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
		Check(l, "acd", "A=acd"); Check(l, "acbed", "A!b _!e _!d");
		Check(l, "abbccd", "A!c A=abbccd"); Check(l, "abbcbed", "A!c A=abbcbed");
		Check(l, "aecd", "A!e A=aecd");
	}

	[TestMethod]
	public void LexSkip5()
	{
		var l = new Ler("A=a *+c|b+e d");
		Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
		Check(l, "acd", "A!d A!"); Check(l, "acbed", "A=acbed");
		Check(l, "abbccd", "A!c A!d A!"); Check(l, "abbcbed", "A!c A=abbcbed");
		Check(l, "aebed", "A!e A=aebed");
	}

	[TestMethod]
	public void LexSkip6()
	{
		Throw(() => new Ler(new LexGram<Tag>().prod(Tag.A).skip["a"].part["b"]), "Skip");
		Throw(() => new Ler("A=*a b c"), "");
		Throw(() => new Ler("A=a *|b c"), "");
	}

	[TestMethod]
	public void LexEsc1()
	{
		var l = new Ler(@"A=[\^\-\[\]\\\Z]");
		Check(l, "^", "A=^"); Check(l, "-", "A=-"); Check(l, "[", "A=["); Check(l, "]", "A=]");
		Check(l, "\\", "A=\\"); Check(l, "Z", "A=Z");
	}

	[TestMethod]
	public void LexEsc2()
	{
		var l = new Ler(@"A=\[\]");
		Check(l, "[]", "A=[]");
	}

	[TestMethod]
	public void LexUtf1()
	{
		Throw(() => new Ler("A=\\u+ \n B=\\u\\A"), "Prefix.* of B.1 and A.1");
		Throw(() => new Ler("A=\\u+ \n B=\\u\\u"), "B.1 and A.1 conflict");
		Throw(() => new Ler("A=\\u+ \n B=\\A"), "Prefix.* of B.1 and A.1");
	}

	[TestMethod]
	public void LexUtf2()
	{
		var l = new Ler("A=a\\u\\u\\uz|a\\u\\uz \n B=a");
		Check(l, "a你za好z", "A=a你z A=a好z");
		Check(l, "a\x80z", "A=a\x80z");
	}

	[TestMethod]
	public void LexUtf3()
	{
		var l = new Ler("A=a[\\u\\A^z]+z \n B=[a\\u^\\u]");
		Check(l, "a好za大家\t都好z", "A=a好z A=a大家\t都好z");
		Check(l, "a", "B=a");
		var bs = Encoding.UTF8.GetBytes("a好z"); bs[2] = 0;
		Check(l, bs, "A=" + Encoding.UTF8.GetString(bs));
	}
}
