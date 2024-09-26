//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

[TestClass]
public class TestLexier : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	readonly Lexier ler = new();

	void Check(string read, string s)
	{
		if (Debugger.IsAttached)
			ler.dump = true;
		env.WriteLine(read);
		using var __ = ler.Begin(new LerByte(Encoding.UTF8.GetBytes(read)));
		while (ler.Next()) ;
		var ls = ler.Dumper();
		env.WriteLine(ls);
		AreEqual(s, ls);
	}

	void CheckSp(string read, string s)
	{
		ler.blanks = [];
		try {
			Check(read, s);
		}
		finally {
			ler.blanks = null;
		}
	}

	[TestMethod]
	public void LexGroup()
	{
		AreEqual("Bin ORB XORB BinK OR XOR AND EQ UEQ LEQ GEQ LT GT ADD SUB MUL DIV MOD DIVF MODF SHL SHR ANDB",
			string.Join(" ", LexIs.OfGroup(Lex.Bin).Order()));
		AreEqual("BIN3 BIN4 BIN53 BIN57 BIN67 "
			+ "Bin ORB XORB BinK OR XOR AND EQ UEQ LEQ GEQ LT GT ADD SUB MUL DIV MOD DIVF MODF SHL SHR ANDB",
			string.Join(" ", LexIs.OfGroup(Lex.Bin, true).Order()));
	}

	[TestMethod]
	public void Comment()
	{
		CheckSp("\\##\\\\###\\ ", "COMB? SP? EOL?");
		CheckSp("\\### \\# ###\\ ###\\ ab", "COMB? SP? COM? EOL?");
		CheckSp("\\## \\## ##\\ ##\\ \nab", "COMB? SP? COM? EOL? NAME=ab EOL");
		Check("\\## \\## ##\\ ##\\ \nab", "NAME=ab EOL");
		Check("\\", "COMB!"); Check("\\\\", "COMB!");
	}

	[TestMethod]
	public void Eol()
	{
		CheckSp("\n\\##\\\t \r\n\r\n\\##\\ \t \n",
			@"EOL? COMB? SP? EOL!use LF \n eol instead of CRLF \r\n EOL? EOL? COMB? SP? EOL?");
		Check("\\##\\\t \r\n\r\n\\##\\ \t \n", @"EOL!use LF \n eol instead of CRLF \r\n");
	}

	[TestMethod]
	public void Eor()
	{
		CheckSp("", "EOL?");
		CheckSp("a  ", "NAME=a SP? EOL");
		CheckSp("a\n", "NAME=a EOL");
		CheckSp("a\n  ", "NAME=a EOL EOL?");
		CheckSp("1\n  a", "INT=1 EOL IND=2 NAME=a EOL DED=2");
		CheckSp("1\n  a  ", "INT=1 EOL IND=2 NAME=a SP? EOL DED=2");
		CheckSp("1\n  a\n", "INT=1 EOL IND=2 NAME=a EOL DED=2");
	}

	[TestMethod]
	public void Indent1()
	{
		CheckSp("a \t", "NAME=a SP? EOL");
		CheckSp("\t    ", "SP? SP!do not mix tabs and spaces for indent EOL?");
		CheckSp(" \n\t", "EOL? SP!do not mix tabs and spaces for indent SP?");
		CheckSp("  \n      \n  \n  \n", "EOL? EOL? EOL? EOL?");
	}

	[TestMethod]
	public void Indent2()
	{
		CheckSp("""
				a
				b
						c
							d
						e
					f
			g
			h
			""",
		"EOL IND=4 NAME=a EOL NAME=b EOL INDR=12 NAME=c EOL IND=16 NAME=d EOL DED=16 NAME=e EOL DEDR=12 IND=8 NAME=f EOL DED=8 DED=4 NAME=g EOL NAME=h EOL");
	}

	[TestMethod]
	public void Indent3()
	{
		CheckSp(" a", "NAME=a EOL");
		CheckSp("  a", "EOL IND=2 NAME=a EOL DED=2");
		CheckSp("""
			  a
			  b
			        c
			          d
			        e
			    f
			g
			h
			""",
		"EOL IND=2 NAME=a EOL NAME=b EOL INDR=8 NAME=c EOL IND=10 NAME=d EOL DED=10 NAME=e EOL DEDR=8 IND=4 NAME=f EOL DED=4 DED=2 NAME=g EOL NAME=h EOL");
	}

	[TestMethod]
	public void Indent4()
	{
		CheckSp("    ##\n      ##\n  ##", "COM? EOL? COM? EOL? COM? EOL?");
		Check("    ##\n      ##\n  ##", "");
		Check("""
				a
					|
			|
				b
						|
						c
				|
					|
			d
			""".Replace("|", ""),
		"EOL IND=4 NAME=a EOL NAME=b EOL INDR=12 NAME=c EOL DEDR=12 DED=4 NAME=d EOL");
		Check("""
					### |
				a |
			\##\  b
					\###\	c
			##
						d
			""".Replace("|", ""),
		"EOL IND=4 NAME=a EOL DED=4 NAME=b EOL INDR=8 NAME=c EOL IND=12 NAME=d EOL DED=12 DEDR=8");
	}

	[TestMethod]
	public void Indent5()
	{
		Check("""
			          a
			      b
			        c
			  d
			e
			""",
		"EOL INDR=10 NAME=a EOL INDR!indent-right expected same as upper lines NAME=b EOL IND=8 NAME=c EOL DED=8 DEDR=6 IND=2 NAME=d EOL DED=2 NAME=e EOL");
		CheckSp("""
   			   a
   			  b
   			    c
   			     d
   			 e
   			""",
		"EOL IND=3 NAME=a EOL NAME=b EOL NAME=c EOL IND=5 NAME=d EOL DED=5 DED=3 NAME=e EOL");
	}

	[TestMethod]
	public void Utf()
	{
		Check(@"好", "0!\xe5 0!\xa5 0!\xbd");
		Check(@"""abc你好def""", "STR=abc你好def EOL");
	}

	[TestMethod]
	public void String1()
	{
		Check("\"abc  def\"", "STR=abc  def EOL");
		Check("\"a", "STR!"); Check("\"a\nb\"", "STR=a STR!eol unexpected EOL NAME=b STR! EOL");
		var s = string.Join("", Enumerable.Range(1000, 5000).ToArray());
		CheckSp($"\"{s}\"", $"STR={s} EOL");
	}

	[TestMethod]
	public void String2()
	{
		Check(@"""\tabc\r\ndef""", "STR=\tabc\r\ndef EOL");
		Check(@"""\x09abc\x0d\x0adef""", "STR=\tabc\r\ndef EOL");
		Check(@"""abc\\\0\x7edef\""\u597d吗""", "STR=abc\\\0~def\"好吗 EOL");
		Check(@"""\a\x0\uaa""", "STR=ax0uaa STR!\\ STR!\\ STR!\\ EOL");
	}

	[TestMethod]
	public void StringBlock1()
	{
		Check("""\"abcdef"\""", "STRB=abcdef EOL");
		Check("""\\"abcdef"\\""", "STRB=abcdef EOL");
		Check("\\\"a\\tc\ndef\"\\", "STRB=a\\tc\ndef EOL");
	}

	[TestMethod]
	public void StringBlock2()
	{
		Check("""\"ab"cdef"\""", "STRB=ab\"cdef EOL");
		Check("""\""abcdef"\""", "STRB=\"abcdef EOL");
		Check("""\"abcdef""\""", "STRB=abcdef\" EOL");
	}

	[TestMethod]
	public void StringBlock3()
	{
		Check("""\""\\abc"\\def"\\"\""", "STRB=\"\\\\abc\"\\\\def\"\\\\ EOL");
		Check("""\\""\abc"\def"\"\\""", "STRB=\"\\abc\"\\def\"\\ EOL");
	}

	[TestMethod]
	public void Name1()
	{
		Check("abc", "NAME=abc EOL"); Check("Abc123", "NAME=Abc123 EOL");
		Check("_123", "NAME=_123 EOL"); Check("__a4", "NAME=__a4 EOL");
		Check("_A__b_", "NAME=_A__b_ EOL"); Check("_123 __", "NAME=_123 NAME=__ EOL");
	}

	[TestMethod]
	public void Name2()
	{
		Check("a..", "NAME=a RUND RUND EOL");
		Check("1.).", "INT=1 RUND RP RUND EOL");
		Check("A1.b_..c-__.__", "NAME=A1 RUND=b_ RUND RUND=c SUB NAME=__ RUND=__ EOL");
	}

	[TestMethod]
	public void Name3()
	{
		Check("a ..).b", "NAME=a RUN RUND RP RUND=b EOL");
		Check("A1 .b_..c .d", "NAME=A1 RUN=b_ RUND RUND=c RUN=d EOL");
	}

	[TestMethod]
	public void Name4()
	{
		Check("a234567890123456789012345678901234567890", "NAME=a234567890123456789012345678901234567890 EOL");
		Check("a2345678901234567890123456789012345678901", "NAME!too long NAME=a234567890123456789012345678901234567890 EOL");
	}

	[TestMethod]
	public void Path1()
	{
		Check("``", "NAME=, EOL"); Check("`abc  def`", "NAME=abc  def, EOL");
		Check("`a", "NAME!"); Check("`a\nb`", "NAME=a, NAME!eol unexpected EOL NAME=b NAME! EOL");
		Check(@"`\tabc\`..\.\ndef.`", "NAME=\tabc`,,.\ndef,, EOL");
		Check(@"`..\x09abc\x0adef`", "NAME=,,\tabc\ndef, EOL");
		Check(@"`abc\\\0\x7edef\u597d吗\x2e`", "NAME=abc\\\0~def好吗., EOL");
		Check(@"`1.2\a\x0\uaa`", "NAME=1,2ax0uaa, NAME!\\ NAME!\\ NAME!\\ EOL");
	}

	[TestMethod]
	public void Path2()
	{
		Check("a.``", "NAME=a RUND=, EOL");
		Check("a .`a\n", "NAME=a RUN=a, NAME!eol unexpected EOL");
		Check("a .`a`.b.`c`", "NAME=a RUN=a, RUND=b RUND=c, EOL");
		Check("a .`b.b`.`c` .`d`.``", "NAME=a RUN=b,b, RUND=c, RUN=d, RUND=, EOL");
	}

	[TestMethod]
	public void Hex1()
	{
		Check("0x0", "INT=0 EOL"); Check("0xF", "INT=15 EOL"); Check("0xx", "HEX!x");
		Check("+0x0A", "POSI INT=10 EOL"); Check("-0x0A", "NEGA INT=10 EOL");
		Check("0x_0", "INT=0 EOL"); Check("0x__0", "INT=0 EOL"); Check("0x_0_", "HEX!");
		Check("1x1", "INT=1 NAME!literal can not densely follow literal NAME=x1 EOL");
	}

	[TestMethod]
	public void Hex2()
	{
		Check("0x7fffffff", "INT=2147483647 EOL"); Check("0x80_00_00_00", "INT=2147483648 EOL");
		Check("0xffff_fffe", "INT=4294967294 EOL"); Check("0xffff_fffe_", "HEX!");
	}

	[TestMethod]
	public void Int1()
	{
		Check("0", "INT=0 EOL"); Check("0 a", "INT=0 NAME=a EOL");
		Check("+09", "POSI INT=9 EOL"); Check("-9876", "NEGA INT=9876 EOL");
		Check("2_", "NUM!"); Check("23__3", "INT=233 EOL");
	}

	[TestMethod]
	public void Int2()
	{
		Check("2_14_7483_648", "INT=2147483648 EOL");
		Check("+214_7483_649", "POSI INT!integer out of range INT=0 EOL");
	}

	[TestMethod]
	public void Float1()
	{
		Check("0.000f", "FLOAT=0 EOL"); Check("-0f", "NEGA FLOAT=0 EOL");
		Check("00.0x", "FLOAT=0 NAME!literal can not densely follow literal NAME=x EOL");
		Check("340282347999999999999999999999999999999.99999", "FLOAT=3.4028235E+38 EOL");
		Check("340282430000000000000000000000000000000.5", "FLOAT!float out of range FLOAT=0 EOL");
	}

	[TestMethod]
	public void Float2()
	{
		Check("1234.0", "FLOAT=1234 EOL"); Check("553.2", "FLOAT=553.2 EOL");
		Check("34_8.5", "FLOAT=348.5 EOL"); Check("1.0__0", "FLOAT=1 EOL");
		Check("1.00_0_01000000000000000000000000000000000000000000000001", "FLOAT=1.00001 EOL");
	}

	[TestMethod]
	public void Float3()
	{
		Check("1e38", "FLOAT=1E+38 EOL"); Check("1e+38", "FLOAT=1E+38 EOL");
		Check("5e38", "FLOAT!float out of range FLOAT=0 EOL");
		Check("1e88", "FLOAT!float out of range FLOAT=0 EOL");
		Check("1e999999", "FLOAT!float out of range FLOAT=0 EOL");
		Check("0.0000034e44", "FLOAT=3.4E+38 EOL");
		Check("0.0000035e44", "FLOAT!float out of range FLOAT=0 EOL");
		Check("3402.823479E35", "FLOAT=3.4028235E+38 EOL");
	}

	[TestMethod]
	public void Float4()
	{
		Check("1.234567891e-39", "FLOAT=1.234568E-39 EOL");
		Check("1.999999999e-45", "FLOAT=1E-45 EOL");
		Check("1e-46", "FLOAT=0 EOL"); Check("1e-83787", "FLOAT=0 EOL");
		Check("0.0000000000000000000000000000000000000000000000000034e89f", "FLOAT=3.4E+38 EOL");
		Check("0.0000000000000000000000000000000000000000000000000034e90f", "FLOAT!float out of range FLOAT=0 EOL");
	}

	[TestMethod]
	public void Symbol1()
	{
		Check("#$':;?@^~", "0!# 0!$ 0!' 0!: 0!; 0!? 0!@ 0!^ 0!~");
	}

	[TestMethod]
	public void Symbol2()
	{
		Check("([{)]},", "LP LSB LCB RP RSB RCB INP EOL");
		Check("!*/%//%%", "NOT MUL DIV MOD DIVF MODF EOL");
		Check("!<<>> --&&++||", "NOT SHL SHR NOTB ANDB XORB ORB EOL");
	}

	[TestMethod]
	public void Symbol3()
	{
		Check("!/====<<=<=<>=>", "NOT UEQ EQ QUO SHL QUO LEQ LT GEQ GT EOL");
		Check("!!&!=|", "NOT NOT AND XOR OR EOL");
	}

	[TestMethod]
	public void BinaryPrefix()
	{
		Check("+-1 +a- b + 2-", "POSI NEGA INT=1 POSI NAME=a SUB NAME=b ADD INT=2 SUB EOL");
		Check("+\n1+- 2\\##\\+- a +", "EOL INDJ=3 ADD EOL DED=3 INT=1 ADD SUB INT=2 POSI SUB NAME=a ADD EOL");
		Check("(+1 -)+2 +\n-3", "LP POSI INT=1 NEGA RP ADD INT=2 ADD EOL NEGA INT=3 EOL");
	}

	[TestMethod]
	public void Junct1()
	{
		CheckSp("""
			a
			*\##\1
				4
			""",
		"NAME=a EOL INDJ=3 MUL EOL COMB? INT=1 EOL INT=4 EOL DED=3");
		CheckSp("""
			a
			 *
			##
			    1
			    *4
			""",
		"NAME=a EOL INDJ=4 MUL EOL EOL? COM? EOL? INT=1 EOL INDJ=7 MUL EOL INT=4 EOL DED=7 DED=4");
	}

	[TestMethod]
	public void Junct2()
	{
		CheckSp("* 1", "EOL INDJ=3 MUL EOL SP? INT=1 EOL DED=3");
		CheckSp("  * 1", "EOL IND=2 EOL INDJ=5 MUL EOL SP? INT=1 EOL DED=5 DED=2");
		CheckSp("  * 1\n* 2", "EOL IND=2 EOL INDJ=5 MUL EOL SP? INT=1 EOL DED=5 DED=2 INDJ=3 MUL EOL SP? INT=2 EOL DED=3");
		CheckSp("+\n1", "EOL INDJ=3 ADD EOL EOL? DED=3 INT=1 EOL");
		CheckSp("*\n1", "EOL INDJ=3 MUL EOL EOL? DED=3 INT=1 EOL");
	}

	[TestMethod]
	public void Junct3()
	{
		Check("""
			1
				- *2
				. /2
				3
			""",
		"INT=1 EOL IND=4 EOL INDJ=7 SUB EOL MUL INT=2 EOL DED=7 INDJ=7 RUN EOL DIV INT=2 EOL DED=7 INT=3 EOL DED=4");
		Check("""
			a
			*1
						2
					3
				*
						4
			5
			""",
		"NAME=a EOL INDJ=3 MUL EOL INT=1 EOL INDR=12 INT=2 EOL DEDR=12 IND=8 INT=3 EOL DED=8 INDJ=7 MUL EOL EOL IND=12 INT=4 EOL DED=12 DED=7 DED=3 INT=5 EOL");
	}

	[TestMethod]
	public void Junct4()
	{
		CheckSp("""
			1
			[a]
				*2
			""",
		"INT=1 EOL INDJ=3 LSB NAME=a RSB EOL INDJ=7 MUL EOL INT=2 EOL DED=7 DED=3");
		CheckSp(""""
			1
			..
				*2
			"""",
		"INT=1 EOL INDJ=3 RUN RUN EOL EOL? EOL INDJ=7 MUL EOL INT=2 EOL DED=7 DED=3");
		CheckSp("..........", "EOL INDJ=3 RUN RUN RUN RUN RUN RUN RUN RUN RUN RUN EOL DED=3 EOL?");
	}
}
