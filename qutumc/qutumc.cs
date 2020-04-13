//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using qutum.syntax;
using System;
using System.IO;

namespace qutum.compile
{
	static class Qutumc
	{
		static void Main(string[] args)
		{
			using var env = EnvWriter.Begin(true);
			try {
				string file;
				if (args.Length > 0)
					file = args[0];
				else {
					env.Write("Qutum file ? "); env.Flush();
					file = Console.ReadLine();
				}
				var bs = File.ReadAllBytes(file);
				var p = new Parser(new Lexer { allValue = true });
				using var __ = p.scan.Load(new ScanByte(bs));
				var top = p.Parse();
				top.Dump((Func<int, int, (int, int, int, int)>)p.scan.LineCol);
			}
			catch (Exception e) {
				env.WriteLine(e);
			}
		}
	}
}
