//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using qutum.parser;
using qutum.syntax;
using System;
using System.IO;

namespace qutum.compile;

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
			var p = new Synter(new Lexier { allValue = true });
			using var __ = p.ler.Begin(new LerByte(bs));
			var top = p.Parse();
			top.Dump((Func<int, int, (int, int, int, int)>)p.ler.LineCol);
		}
		catch (Exception e) {
			env.WriteLine(e);
		}
	}
}
