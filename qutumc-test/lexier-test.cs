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
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

[TestClass]
public class TestLexier : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	readonly Lexier ler = new() { eor = false, errs = null };

	void Check(string read, string s)
	{
		env.WriteLine(read);
		using var __ = ler.Begin(new LerByte(Encoding.UTF8.GetBytes(read)));
		while (ler.Next())
			;
		var ls = string.Join(" ", ler.Lexs(1, ler.Loc()).Select(t => t.Dumper(Dump)).ToArray());
		env.WriteLine(ls);
		AreEqual(s, ls);
	}

	static string Dump(object d) => d is object[] s ? string.Join(',', s) + "," : d?.ToString() ?? "";

	void CheckSp(string read, string s)
	{
		ler.allBlank = true;
		try {
			Check(read, s);
		}
		finally {
			ler.allBlank = false;
		}
	}

	[TestMethod]
	public void LexGroup()
	{
		AreEqual("Bin BIN1 BIN2 BIN3 BIN4 BIN53 BIN57 BIN67 BIN7 ORB XORB BinK "
			+ "OR XOR AND EQ UEQ LEQ GEQ LT GT ADD SUB MUL DIV MOD DIVF MODF SHL SHR ANDB",
			string.Join(" ", LexIs.OfGroup(Lex.Bin).Order()));
	}

	[TestMethod]
	public void Comment()
	{
		CheckSp("\\##\\\\###\\ ", "COMB= SP=");
		CheckSp("\\### \\# ###\\ ###\\ ab", "COMB= SP= COM=");
		CheckSp("\\## \\## ##\\ ##\\ \nab", "COMB= SP= COM= EOL= NAME=ab");
		Check("\\## \\## ##\\ ##\\ \nab", "NAME=ab");
		Check("\\", "COMB!"); Check("\\\\", "COMB!");
	}

	[TestMethod]
	public void Eol()
	{
		CheckSp("\\##\\\t \r\n\r\n\\##\\ \t \n",
			@"COMB= SP= EOL!use LF \n eol instead of CRLF \r\n EOL= EOL= COMB= SP= EOL=");
		Check("\\##\\\t \r\n\r\n\\##\\ \t \n", @"EOL!use LF \n eol instead of CRLF \r\n");
	}

	[TestMethod]
	public void Indent1()
	{
		CheckSp("a \t", "NAME=a SP=");
		CheckSp("\t    ", "SP!do not mix tabs and spaces for indent SP=");
		CheckSp(" \n\t", "EOL= SP!do not mix tabs and spaces for indent SP=");
		CheckSp("\t\n\t\t\t\n\t\n\t\n", "EOL= EOL= EOL= EOL=");
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
		""", "IND=4 NAME=a EOL= NAME=b EOL= INDR=12 NAME=c EOL= IND=16 NAME=d EOL= DED=16 NAME=e EOL= DEDR=12 IND=8 NAME=f EOL= DED=8 DED=4 NAME=g EOL= NAME=h");
	}

	[TestMethod]
	public void Indent3()
	{
		CheckSp(" a", "NAME=a"); CheckSp("  a", "IND=2 NAME=a DED=2");
		CheckSp("""
		  a
		  b
		        c
		          d
		        e
		    f
		g
		h
		""", "IND=2 NAME=a EOL= NAME=b EOL= INDR=8 NAME=c EOL= IND=10 NAME=d EOL= DED=10 NAME=e EOL= DEDR=8 IND=4 NAME=f EOL= DED=4 DED=2 NAME=g EOL= NAME=h");
	}

	[TestMethod]
	public void Indent4()
	{
		CheckSp("\t\t##\n\t\t\t##\n\t##", "INDR=8 COM= EOL= IND=12 COM= EOL= DED=12 DEDR=8 IND=4 COM= DED=4");
		Check("\t\t##\n\t\t\t##\n\t##", "");
		Check("""
			a
				,
		,
			b
					,
					c
		
			,
		d
		""".Replace(",", ""), "IND=4 NAME=a EOL= NAME=b EOL= INDR=12 NAME=c EOL= DEDR=12 DED=4 NAME=d");
		Check("""
				### ,
			a ,
		\##\  b
				\###\	c
		##
					d
		""".Replace(",", ""), "IND=4 NAME=a EOL= DED=4 NAME=b EOL= INDR=8 NAME=c EOL= IND=12 NAME=d DED=12 DEDR=8");
	}

	[TestMethod]
	public void Indent5()
	{
		Check("          a\n      b\n  c",
			"INDR=10 NAME=a EOL= INDR!indent-right expected same as upper lines NAME=b EOL= DEDR=10 IND=2 NAME=c DED=2");
		CheckSp("   a\n  b\n    c\n     d\n  e",
			"IND=3 NAME=a EOL= NAME=b EOL= NAME=c EOL= IND=5 NAME=d EOL= DED=5 NAME=e DED=3");
		ler.leadInd = false;
		Check("          a\n      b\n  c", "NAME=a EOL= NAME=b EOL= NAME=c");
		CheckSp("   a\n  b\n    c\n     d\n  e",
			"NAME=a EOL= NAME=b EOL= NAME=c EOL= IND=5 NAME=d EOL= DED=5 NAME=e");
	}

	[TestMethod]
	public void Eor1()
	{
		CheckSp("a\t", "NAME=a SP=");
		CheckSp("\ta\t", "IND=4 NAME=a SP= DED=4");
		CheckSp("a\n\t", "NAME=a EOL=");
		CheckSp("\ta\n", "IND=4 NAME=a EOL= DED=4");
		Check("a\t", "NAME=a");
		Check("\ta\t", "IND=4 NAME=a DED=4");
		Check("a\n\t\t\n\t", "NAME=a EOL=");
		Check("\ta\n\t\t\n", "IND=4 NAME=a EOL= DED=4");
	}

	[TestMethod]
	public void Eor2()
	{
		ler.eor = true;
		CheckSp("a\n", "NAME=a EOL= EOL=");
		CheckSp("a\t", "NAME=a SP= EOL=");
		CheckSp("\ta", "IND=4 NAME=a EOL= DED=4");
		CheckSp("\ta\t", "IND=4 NAME=a SP= EOL= DED=4");
		CheckSp("a\n\t", "NAME=a EOL= EOL=");
		CheckSp("\ta\n", "IND=4 NAME=a EOL= EOL= DED=4");
		Check("a\n", "NAME=a EOL=");
		Check("a\t", "NAME=a EOL=");
		Check("\ta", "IND=4 NAME=a EOL= DED=4");
		Check("\ta\t", "IND=4 NAME=a EOL= DED=4");
		Check("a\n\t\t\n\t", "NAME=a EOL=");
		Check("\ta\n\t\t\n", "IND=4 NAME=a EOL= DED=4");
	}

	[TestMethod]
	public void Utf()
	{
		Check(@"好", "0!\xe5 0!\xa5 0!\xbd");
		Check(@"""abc你好def""", "STR=abc你好def");
	}

	[TestMethod]
	public void String1()
	{
		Check("\"abc  def\"", "STR=abc  def");
		Check("\"a", "STR!"); Check("\"a\nb\"", "STR!eol unexpected STR=a EOL= NAME=b STR!");
		var s = string.Join("", Enumerable.Range(1000, 5000).ToArray());
		CheckSp($"\"{s}\"", $"STR={s}");
	}

	[TestMethod]
	public void String2()
	{
		Check(@"""\tabc\r\ndef""", "STR=\tabc\r\ndef");
		Check(@"""\x09abc\x0d\x0adef""", "STR=\tabc\r\ndef");
		Check(@"""abc\\\0\x7edef\""\u597d吗""", "STR=abc\\\0~def\"好吗");
		Check(@"""\a\x0\uaa""", "STR!\\ STR!\\ STR!\\ STR=ax0uaa");
	}

	[TestMethod]
	public void StringBlock1()
	{
		Check("""\"abcdef"\""", "STRB=abcdef"); Check("""\\"abcdef"\\""", "STRB=abcdef");
		Check("\\\"a\\tc\ndef\"\\", "STRB=a\\tc\ndef");
	}

	[TestMethod]
	public void StringBlock2()
	{
		Check("""\"ab"cdef"\""", "STRB=ab\"cdef");
		Check("""\""abcdef"\""", "STRB=\"abcdef");
		Check("""\"abcdef""\""", "STRB=abcdef\"");
	}

	[TestMethod]
	public void StringBlock3()
	{
		Check("""\""\\abc"\\def"\\"\""", "STRB=\"\\\\abc\"\\\\def\"\\\\");
		Check("""\\""\abc"\def"\"\\""", "STRB=\"\\abc\"\\def\"\\");
	}

	[TestMethod]
	public void Name1()
	{
		Check("abc", "NAME=abc"); Check("Abc123", "NAME=Abc123");
		Check("_123", "NAME=_123"); Check("__a4", "NAME=__a4"); Check("_A__b_", "NAME=_A__b_");
		Check("_123 __", "NAME=_123 NAME=__");
	}

	[TestMethod]
	public void Name2()
	{
		Check("a..", "NAME=a RUND= RUND=");
		Check("1.).", "INT=1 RUND= RP= RUND=");
		Check("A1.b_..c-__.__", "NAME=A1 RUND=b_ RUND= RUND=c SUB= NAME=__ RUND=__");
	}

	[TestMethod]
	public void Name3()
	{
		Check("a ..).b", "NAME=a RUN= RUND= RP= RUND=b");
		Check("A1 .b_..c .d", "NAME=A1 RUN=b_ RUND= RUND=c RUN=d");
	}

	[TestMethod]
	public void Name4()
	{
		Check("a234567890123456789012345678901234567890", "NAME=a234567890123456789012345678901234567890");
		Check("a2345678901234567890123456789012345678901", "NAME!too long NAME=a234567890123456789012345678901234567890");
	}

	[TestMethod]
	public void Path1()
	{
		Check("``", "NAME=,"); Check("`abc  def`", "NAME=abc  def,");
		Check("`a", "NAME!"); Check("`a\nb`", "NAME!eol unexpected NAME=a, EOL= NAME=b NAME!");
		Check(@"`\tabc\`..\.\ndef.`", "NAME=\tabc`,,.\ndef,,");
		Check(@"`..\x09abc\x0adef`", "NAME=,,\tabc\ndef,");
		Check(@"`abc\\\0\x7edef\u597d吗\x2e`", "NAME=abc\\\0~def好吗.,");
		Check(@"`1.2\a\x0\uaa`", "NAME!\\ NAME!\\ NAME!\\ NAME=1,2ax0uaa,");
	}

	[TestMethod]
	public void Path2()
	{
		Check(".``", "RUN=,");
		Check(".`a\n", "NAME!eol unexpected RUN=a, EOL=");
		Check(".`a`.b.`c`", "RUN=a, RUND=b RUND=c,");
		Check("a .`b.b`.`c` .`d`.``", "NAME=a RUN=b,b, RUND=c, RUN=d, RUND=,");
	}

	[TestMethod]
	public void Hex1()
	{
		Check("0x0", "INT=0"); Check("0xF", "INT=15"); Check("0xx", "HEX!x");
		Check("+0x0A", "POSI= INT=10"); Check("-0x0A", "NEGA= INT=10");
		Check("0x_0", "INT=0"); Check("0x__0", "INT=0"); Check("0x_0_", "HEX!");
		Check("1x1", "INT=1 NAME!literal can not densely follow literal NAME=x1");
	}

	[TestMethod]
	public void Hex2()
	{
		Check("0x7fffffff", "INT=2147483647"); Check("0x80_00_00_00", "INT=2147483648");
		Check("0xffff_fffe", "INT=4294967294"); Check("0xffff_fffe_", "HEX!");
	}

	[TestMethod]
	public void Int1()
	{
		Check("0", "INT=0"); Check("0 a", "INT=0 NAME=a");
		Check("+09", "POSI= INT=9"); Check("-9876", "NEGA= INT=9876");
		Check("2_", "NUM!"); Check("23__3", "INT=233");
	}

	[TestMethod]
	public void Int2()
	{
		Check("2_14_7483_648", "INT=2147483648");
		Check("+214_7483_649", "POSI= INT!integer out of range INT=0");
	}

	[TestMethod]
	public void Float1()
	{
		Check("0.000f", "FLOAT=0"); Check("-0f", "NEGA= FLOAT=0");
		Check("00.0x", "FLOAT=0 NAME!literal can not densely follow literal NAME=x");
		Check("340282347999999999999999999999999999999.99999", "FLOAT=3.4028235E+38");
		Check("340282430000000000000000000000000000000.5", "FLOAT!float out of range FLOAT=0");
	}

	[TestMethod]
	public void Float2()
	{
		Check("1234.0", "FLOAT=1234"); Check("553.2", "FLOAT=553.2"); Check("34_8.5", "FLOAT=348.5");
		Check("1.00_0_01000000000000000000000000000000000000000000000001", "FLOAT=1.00001");
		Check("1.0__0", "FLOAT=1");
	}

	[TestMethod]
	public void Float3()
	{
		Check("1e38", "FLOAT=1E+38"); Check("1e+38", "FLOAT=1E+38");
		Check("5e38", "FLOAT!float out of range FLOAT=0");
		Check("1e88", "FLOAT!float out of range FLOAT=0");
		Check("1e999999", "FLOAT!float out of range FLOAT=0");
		Check("0.0000034e44", "FLOAT=3.4E+38");
		Check("0.0000035e44", "FLOAT!float out of range FLOAT=0");
		Check("3402.823479E35", "FLOAT=3.4028235E+38");
	}

	[TestMethod]
	public void Float4()
	{
		Check("1.234567891e-39", "FLOAT=1.234568E-39");
		Check("1.999999999e-45", "FLOAT=1E-45");
		Check("1e-46", "FLOAT=0"); Check("1e-83787", "FLOAT=0");
		Check("0.0000000000000000000000000000000000000000000000000034e89f", "FLOAT=3.4E+38");
		Check("0.0000000000000000000000000000000000000000000000000034e90f", "FLOAT!float out of range FLOAT=0");
	}

	[TestMethod]
	public void Symbol1()
	{
		Check("#$':;?@^~", "0!# 0!$ 0!' 0!: 0!; 0!? 0!@ 0!^ 0!~");
	}

	[TestMethod]
	public void Symbol2()
	{
		Check("([{)]},", "LP= LSB= LCB= RP= RSB= RCB= INP=");
		Check("*/%//%%", "MUL= DIV= MOD= DIVF= MODF=");
		Check("<<>> ---&&++||", "SHL= SHR= NOTB= NEGA= ANDB= XORB= ORB=");
	}

	[TestMethod]
	public void Symbol3()
	{
		Check("===/=<<=<=<>=>", "EQ= QUO= UEQ= SHL= QUO= LEQ= LT= GEQ= GT=");
		Check("!!&!=|", "NOT= NOT= AND= XOR= OR=");
	}

	[TestMethod]
	public void BinaryPrefix()
	{
		Check("+-1 +a- b + 2-", "POSI= NEGA= INT=1 POSI= NAME=a SUB= NAME=b ADD= INT=2 SUB=");
		Check("+\n1+- 2\\##\\+- a +", "ADD= EOL= INT=1 ADD= SUB= INT=2 POSI= SUB= NAME=a ADD=");
		Check("(+1 -)+2 +\n-3", "LP= POSI= INT=1 NEGA= RP= ADD= INT=2 ADD= EOL= NEGA= INT=3");
	}
}
