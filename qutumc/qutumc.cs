//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

#pragma warning disable IDE1006 // Naming Styles
using qutum.syntax;
using System;
using System.Collections.Generic;
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
				var lexer = new Lexer { allValue = true };
				var top = new Parsers(lexer) { treeDump = 1 }.Parse(ReadFile(file));
				top.Dump((Func<int, int, (int, int, int, int)>)lexer.LineCol);
			}
			catch (Exception e) {
				env.WriteLine(e);
			}
		}

		static IEnumerable<byte> ReadFile(string file)
		{
			using var f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
			var bs = new byte[16384];
			for (int n; (n = f.Read(bs, 0, bs.Length)) > 0;)
				for (int x = 0; x < n; x++)
					yield return bs[x];
		}
	}
}
