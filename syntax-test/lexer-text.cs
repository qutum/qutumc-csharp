//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.syntax;
using System;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax
{
	[TestClass]
	public class TestLexer
	{
		public TestLexer() { DebugWriter.ConsoleBegin(); }

		void Check(Lexer l, string input, string s) => Check(l, Encoding.UTF8.GetBytes(input), s);

		void Check(Lexer l, byte[] input, string s)
		{
			l.Load(input);
			while (l.Next()) ;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.Dump()).ToArray());
			Console.WriteLine(z);
			l.Unload();
			AreEqual(s, z);
		}

		[TestMethod]
		public void String1()
		{
			var l = new Lexer();
			Check(l, @"""abcÄãºÃdef""", "Str=abcÄãºÃdef");
			Check(l, @"""a", "Str!"); Check(l, "\"a\nb\"", "Str!\n Str=ab");
		}

		[TestMethod]
		public void String2()
		{
			var l = new Lexer();
			Check(l, @"""\tabc\r\ndef""", "Str=\tabc\r\ndef");
			Check(l, @"""\x09abc\x0d\x0adef""", "Str=\tabc\r\ndef");
			Check(l, @"""\abc\\\0\x7edef\""\u597dÂð""", "Str=abc\\\0~def\"ºÃÂð");
			Check(l, @"""\x0\uaa""", "Str!\\ Str!\\ Str=x0uaa");
		}

		[TestMethod]
		public void BlockString1()
		{
			var l = new Lexer();
			Check(l, @"\""abcdef""\", "Bstr=abcdef"); Check(l, @"\\""abcdef""\\", "Bstr=abcdef");
			Check(l, "\\\"a\\tc\ndef\"\\", "Bstr=a\\tc\ndef");
		}

		[TestMethod]
		public void BlockString2()
		{
			var l = new Lexer();
			Check(l, @"\""ab""cdef""\", "Bstr=ab\"cdef");
			Check(l, @"\""""abcdef""\", "Bstr=\"abcdef");
			Check(l, @"\""abcdef""""\", "Bstr=abcdef\"");
		}

		[TestMethod]
		public void BlockString3()
		{
			var l = new Lexer();
			Check(l, @"\""""\\abc""\\def""\\""\", "Bstr=\"\\\\abc\"\\\\def\"\\\\");
			Check(l, @"\\""""\abc""\def""\""\\", "Bstr=\"\\abc\"\\def\"\\");
		}
	}
}
