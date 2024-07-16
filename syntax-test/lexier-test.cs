//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
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

	readonly Lexier ler = new() { eof = false, errs = null };

	void Check(string input, string s)
	{
		env.WriteLine(input);
		using var __ = ler.Begin(new LerByte(Encoding.UTF8.GetBytes(input)));
		while (ler.Next())
			;
		var z = string.Join(" ", ler.Lexs(0, ler.Loc()).Select(t => t.ToString(Dump)).ToArray());
		env.WriteLine(z);
		AreEqual(s, z);
	}

	static string Dump(object v) => v is object[] s ? string.Join(',', s) + "," : v?.ToString() ?? "";

	void CheckSp(string input, string s)
	{
		ler.allBlank = true;
		try {
			Check(input, s);
		}
		finally {
			ler.allBlank = false;
		}
	}

	[TestMethod]
	public void LexComm()
	{
		CheckSp("\\##\\\\###\\ ", "COMM=COMMB SP=");
		CheckSp("\\### \\# ###\\ ###\\ ab", "COMM=COMMB SP= COMM=");
		CheckSp("\\## \\## ##\\ ##\\ \nab", "COMM=COMMB SP= COMM= EOL= NAME=ab");
		Check("\\## \\## ##\\ ##\\ \nab", "NAME=ab");
		Check("\\\\", "COMMB!");
	}

	[TestMethod]
	public void LexEol()
	{
		CheckSp("\\##\\\t \r\n\r\n\\##\\ \t \n",
			@"COMM=COMMB SP= EOL!use LF \n eol instead of CRLF \r\n EOL= EOL= COMM=COMMB SP= EOL=");
		Check("\\##\\\t \r\n\r\n\\##\\ \t \n", @"EOL!use LF \n eol instead of CRLF \r\n");
	}

	[TestMethod]
	public void LexIndent1()
	{
		CheckSp("a \t", "NAME=a SP=");
		CheckSp("\t    ", "SP!do not mix tabs and spaces for indent SP=");
		CheckSp(" \n\t", "EOL= SP!do not mix tabs and spaces for indent SP=");
		CheckSp("\t\n\t\t\t\n\t\n\t\n", "EOL= EOL= EOL= EOL=");
	}

	[TestMethod]
	public void LexIndent2()
	{
		CheckSp("\ta\n\tb\n\t\t\tc\n\t\t\t\td\n\t\t\te\n\t\tf\ng\nh",
			"IND=4 NAME=a EOL= NAME=b EOL= INDR=12 NAME=c EOL= IND=16 NAME=d EOL= DED=16 NAME=e EOL= DEDR=12 IND=8 NAME=f EOL= DED=8 DED=4 NAME=g EOL= NAME=h");
		Check("          a\n      b\n  c",
			"INDR=10 NAME=a EOL= INDR!indent same as upper lines expected NAME=b EOL= DEDR=10 IND=2 NAME=c DED=2");
	}

	[TestMethod]
	public void LexIndent3()
	{
		CheckSp(" a", "NAME=a"); CheckSp("  a", "IND=2 NAME=a DED=2");
		CheckSp("  a\n  b\n        c\n          d\n        e\n    f\ng\nh",
			"IND=2 NAME=a EOL= NAME=b EOL= INDR=8 NAME=c EOL= IND=10 NAME=d EOL= DED=10 NAME=e EOL= DEDR=8 IND=4 NAME=f EOL= DED=4 DED=2 NAME=g EOL= NAME=h");
		CheckSp("   a\n  b\n    c\n     d\n  e",
			"IND=3 NAME=a EOL= NAME=b EOL= NAME=c EOL= IND=5 NAME=d EOL= DED=5 NAME=e DED=3");
	}

	[TestMethod]
	public void LexIndent4()
	{
		CheckSp("\t\t##\n", "INDR=8 COMM= EOL= DEDR=8");
		Check("\n\ta\n\t\t\n\n\tb\n\t\t\t\n\t\t\tc\n\n\t\nd",
			"IND=4 NAME=a EOL= NAME=b EOL= INDR=12 NAME=c EOL= DEDR=12 DED=4 NAME=d");
		Check("\t\t### \n\ta \n\\##\\  b\n\t\t\\###\\\tc",
			"IND=4 NAME=a EOL= DED=4 NAME=b EOL= INDR=8 NAME=c DEDR=8");
	}

	[TestMethod]
	public void LexEof1()
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
	public void LexEof2()
	{
		ler.eof = true;
		CheckSp("a\n", "NAME=a EOL= EOL=");
		CheckSp("a\t", "NAME=a SP= EOL=");
		CheckSp("\ta\t", "IND=4 NAME=a SP= EOL= DED=4");
		CheckSp("a\n\t", "NAME=a EOL= EOL=");
		CheckSp("\ta\n", "IND=4 NAME=a EOL= EOL= DED=4");
		Check("a\n", "NAME=a EOL=");
		Check("a\t", "NAME=a EOL=");
		Check("\ta\t", "IND=4 NAME=a EOL= DED=4");
		Check("a\n\t\t\n\t", "NAME=a EOL=");
		Check("\ta\n\t\t\n", "IND=4 NAME=a EOL= DED=4");
	}

	[TestMethod]
	public void LexUtf()
	{
		Check(@"好", "0!\xe5 0!\xa5 0!\xbd");
		Check(@"""abc你好def""", "STR=abc你好def");
	}

	[TestMethod]
	public void LexString1()
	{
		Check("\"abc  def\"", "STR=abc  def");
		Check("\"a", "STR!"); Check("\"a\nb\"", "STR!\" expected STR=a NAME=b STR!");
		var s = string.Join("", Enumerable.Range(1000, 5000).ToArray());
		CheckSp($"\"{s}\"", $"STR={s}");
	}

	[TestMethod]
	public void LexString2()
	{
		Check(@"""\tabc\r\ndef""", "STR=\tabc\r\ndef");
		Check(@"""\x09abc\x0d\x0adef""", "STR=\tabc\r\ndef");
		Check(@"""abc\\\0\x7edef\""\u597d吗""", "STR=abc\\\0~def\"好吗");
		Check(@"""\a\x0\uaa""", "STR!\\ STR!\\ STR!\\ STR=ax0uaa");
	}

	[TestMethod]
	public void LexStringBlock1()
	{
		Check("""\"abcdef"\""", "STRB=abcdef"); Check("""\\"abcdef"\\""", "STRB=abcdef");
		Check("\\\"a\\tc\ndef\"\\", "STRB=a\\tc\ndef");
	}

	[TestMethod]
	public void LexStringBlock2()
	{
		Check("""\"ab"cdef"\""", "STRB=ab\"cdef");
		Check("""\""abcdef"\""", "STRB=\"abcdef");
		Check("""\"abcdef""\""", "STRB=abcdef\"");
	}

	[TestMethod]
	public void LexStringBlock3()
	{
		Check("""\""\\abc"\\def"\\"\""", "STRB=\"\\\\abc\"\\\\def\"\\\\");
		Check("""\\""\abc"\def"\"\\""", "STRB=\"\\abc\"\\def\"\\");
	}

	[TestMethod]
	public void LexName1()
	{
		Check("abc", "NAME=abc"); Check("Abc123", "NAME=Abc123");
		Check("_123", "NAME=_123"); Check("__a4", "NAME=__a4"); Check("_A__b_", "NAME=_A__b_");
		Check("_123 __", "NAME=_123 NAME=__");
	}

	[TestMethod]
	public void LexName2()
	{
		Check("a..", "NAME=a RNAME1= RNAME1=");
		Check("1.).", "INT=1 RNAME1= RP= RNAME1=");
		Check("A1.b_..c!__.__", "NAME=A1 RNAME1=b_ RNAME1= RNAME1=c NOT= NAME=__ RNAME1=__");
	}

	[TestMethod]
	public void LexName3()
	{
		Check("a ..).b", "NAME=a RNAME= RNAME1= RP= RNAME1=b");
		Check("A1 .b_..c .d", "NAME=A1 RNAME=b_ RNAME1= RNAME1=c RNAME=d");
	}

	[TestMethod]
	public void LexName4()
	{
		Check("a234567890123456789012345678901234567890", "NAME=a234567890123456789012345678901234567890");
		Check("a2345678901234567890123456789012345678901", "NAME!too long NAME=a234567890123456789012345678901234567890");
	}

	[TestMethod]
	public void LexPath1()
	{
		Check("``", "NAME=,"); Check("`a", "NAME!");
		Check("`a\nb`", "NAME!` expected NAME=a, NAME=b NAME!");
		Check("`abc  def`", "NAME=abc  def,");
		Check(@"`\tabc\`..\.\ndef.`", "NAME=\tabc`,,.\ndef,,");
		Check(@"`..\x09abc\x0adef`", "NAME=,,\tabc\ndef,");
		Check(@"`abc\\\0\x7edef\u597d吗\x2e`", "NAME=abc\\\0~def好吗.,");
		Check(@"`1.2\a\x0\uaa`", "NAME!\\ NAME!\\ NAME!\\ NAME=1,2ax0uaa,");
	}

	[TestMethod]
	public void LexPath2()
	{
		Check(".``", "RNAME=,");
		Check(".`a\n", "NAME!` expected RNAME=a,");
		Check(".`a`.b.`c`", "RNAME=a, RNAME1=b RNAME1=c,");
		Check("a .`b.b`.`c` .`d`.``", "NAME=a RNAME=b,b, RNAME1=c, RNAME=d, RNAME1=,");
	}

	[TestMethod]
	public void LexHex1()
	{
		Check("0x0", "INT=0"); Check("0xF", "INT=15"); Check("0xx", "HEX!x");
		Check("+0x0A", "ADD= INT=10"); Check("-0x0A", "SUB= INT=10");
		Check("0x_0", "INT=0"); Check("0x__0", "INT=0"); Check("1x1", "INT=1 NAME=x1");
	}

	[TestMethod]
	public void LexHex2()
	{
		Check("0x7fffffff", "INT=2147483647"); Check("0x80_00_00_00", "INT=2147483648");
		Check("0xffff_fffe", "INT=4294967294"); Check("0xffff_fffe_", "HEX!");
	}

	[TestMethod]
	public void LexInt1()
	{
		Check("0", "INT=0"); Check("0a", "INT=0 NAME=a");
		Check("+09", "ADD= INT=9"); Check("-9876", "SUB= INT=9876");
		Check("2_", "NUM!"); Check("23__3", "INT=233");
	}

	[TestMethod]
	public void LexInt2()
	{
		Check("2_14_7483_648", "INT=2147483648");
		Check("+214_7483_649", "ADD= INT!integer out of range INT=0");
	}

	[TestMethod]
	public void LexFloat1()
	{
		Check("-0f", "SUB= FLOAT=0"); Check("00.0x", "FLOAT=0 NAME=x"); Check("0.000f", "FLOAT=0");
		Check("340282347999999999999999999999999999999.99999", "FLOAT=3.4028235E+38");
		Check("340282430000000000000000000000000000000.5", "FLOAT!float out of range FLOAT=0");
	}

	[TestMethod]
	public void LexFloat2()
	{
		Check("1234.0", "FLOAT=1234"); Check("553.2", "FLOAT=553.2"); Check("34_8.5", "FLOAT=348.5");
		Check("1.00_0_01000000000000000000000000000000000000000000000001", "FLOAT=1.00001");
		Check("1.0__0", "FLOAT=1");
	}

	[TestMethod]
	public void LexFloat3()
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
	public void LexFloat4()
	{
		Check("1.234567891e-39", "FLOAT=1.234568E-39");
		Check("1.999999999e-45", "FLOAT=1E-45");
		Check("1e-46", "FLOAT=0"); Check("1e-83787", "FLOAT=0");
		Check("0.0000000000000000000000000000000000000000000000000034e89f", "FLOAT=3.4E+38");
		Check("0.0000000000000000000000000000000000000000000000000034e90f", "FLOAT!float out of range FLOAT=0");
	}

	[TestMethod]
	public void LexSymbol1()
	{
		Check("',@#$^:;?~", "0!' 0!, 0!@ 0!# 0!$ 0!^ 0!: 0!; 0!? 0!~");
	}

	[TestMethod]
	public void LexSymbol2()
	{
		Check("([{)]}", "LP= LSB= LCB= RP= RSB= RCB=");
		Check("*/%//%%", "MUL= DIV= MOD= DIVF= MODF=");
		Check("<<>>---&&+++||", "SHL= SHR= BNOT= SUB= BAND= BXOR= ADD= BOR=");
	}

	[TestMethod]
	public void LexSymbol3()
	{
		Check("===/=<<=<=<>=>", "EQ= BIND= UEQ= SHL= BIND= LEQ= LT= GEQ= GT=");
		Check("!!&|", "NOT= NOT= AND= OR=");
	}
}
