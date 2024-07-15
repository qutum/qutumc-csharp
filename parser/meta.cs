//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser;

using Set = CharSet;

sealed class MetaStr(string input) : LerStr(input)
{
	public override bool Is(char key) => Is(input[loc], key);

	public override bool Is(int loc, char key) => Is(input[loc], key);

	// for meta grammar
	public override bool Is(char t, char key) =>
		key switch {
			'S' => t is ' ' or '\t',   // space
			'W' => t < 127 && Set.W[t],    // word
			'X' => t < 127 && Set.X[t],    // hexadecimal
			'O' => t < 127 && Set.O[t],    // operator
			'G' => t < 127 && Set.G[t],    // operator for grammar
			'E' => t > ' ' && t < 127, // escape
			'I' => t < 127 && Set.I[t],    // single 
			'R' => t < 127 && Set.I[t] && t != '-' && t != '^', // range
			'Q' => t is '?' or '*' or '+',                  // quantifier
			'H' => t >= ' ' && t < 127 && t != '=' && t != '|', // hint
			'V' => t is >= ' ' or '\t',                         // comment
			_ => t == key,
		};

	// for general grammar
	internal static string Unesc(string s, int f, int t, bool lexer = false)
	{
		if (s[f] != '\\')
			return s[f..t];
		char c = s[++f];
		return c switch {
			's' => " ",
			't' => "\t",
			'n' => "\n",
			'r' => "\r",
			'0' => "\0",
			_ => (lexer ? c : '\0') switch {
				'A' => Set.ALL,
				'l' => Set.LINE,
				'd' => Set.DEC,
				'x' => Set.HEX,
				'a' => Set.ALPHA,
				'w' => Set.WORD,
				'u' => "\x80",
				_ => c < 129 ? Set.ONE[c] : c.ToString(),
			}
		};
	}
}

public static class MetaLex
{
	// gram
	// \ prod
	//   \ key
	//   \ part1
	//     \ byte: single/range.../esc (dup) ...
	//     \ alt1 ...
	//       \ byte: single/range.../esc (dup) ...
	//   \ part ...
	//     \ redo
	//     \ loop
	//     \ byte: single/range.../esc (dup) ...
	//     \ alt ...
	//       \ loop
	//       \ byte: single/range.../esc (dup) ...
	// \ prod ...
	static readonly ParserStr meta = new("""
		gram  = eol* prod prods* eol*
		prods = eol+ prod
		prod  = key S*\=S* part1 part* =+
		key   = W+ =+
		part1 = byte+ alt1* =+
		alt1  = \| S* byte+ =+
		part  = S+ redo? loop? byte+ alt* =+
		alt   = \| S* loop? byte+ =+
		redo  = \* | \| S* =+
		loop  = \+ =+
		byte  = I \+? | [range* ^? range*] \+? | \\E \+? =+
		range = R | R-R | \\E =+
		eol   = S* comm? \r?\n S*
		comm  = \=\= V*
		""") {
		greedy = false, tree = false, dump = 0
	};

	public static LexGram<K> Gram<K>(string gram, bool dump = false) where K : struct
		=> Gram(gram, Lexier<K>.Keyz, dump);

	public static LexGram<K> Gram<K>(string gram, Func<string, IEnumerable<K>> Keys, bool dump = false)
	{
		using var input = new MetaStr(gram);
		var top = meta.Begin(input).Parse();
		if (top.err != 0) {
			using var input2 = new MetaStr(gram);
			var dum = meta.dump; meta.dump = 3;
			meta.Begin(input2).Parse().Dump(); meta.dump = dum;
			var e = new Exception(); e.Data["err"] = top;
			throw e;
		}
		LexGram<K> g = new();
		var es = new object[LexGram<K>.AltByteN << 1];
		// build prod
		foreach (var prod in top) {
			var k = Keys(meta.ler.Lexs(prod.head.from, prod.head.to)).Single();
			g.k(k);
			// build part
			prod.Skip(1).Each((p, part) => {
				++part;
				if (p.head.name != "redo") // no backward cross parts
					_ = g.p;
				else if (gram[p.head.from] == '|') // empty alt
					_ = g.p[""];
				else // shift byte and retry part like the begin
					_ = g.redo;
				// build alt
				p.Where(t => t.name.StartsWith("alt")).Prepend(p).Each((a, alt) => {
					++alt;
					// build elems
					var bytes = a.Where(t => t.name == "byte");
					var bn = bytes.Count();
					if (bn > LexGram<K>.AltByteN)
						throw new($"{k}.{part}.{alt} exceeds {LexGram<K>.AltByteN} bytes :"
							+ meta.ler.Lexs(a.from, a.to));
					var en = 0;
					bytes.Each((b, bx) => Byte(gram, k, part, alt, b, es, ref en));
					_ = g[es[0..en]];
					if (a.head.name == "loop" || a.head.next?.name == "loop")
						_ = g.loop;
				});
			});
		}
		if (dump) Dump(top, g);
		return g;
	}

	static void Byte<K>(string gram, K k, int part, int alt, SyntStr b, object[] es, ref int en)
	{
		var x = b.from;
		if (gram[x] == '\\') { // escape bytes
			es[en++] = MetaStr.Unesc(gram, x, b.to, true).Mem();
			x += 2;
		}
		// build range
		else if (gram[x] == '[') {
			++x;
			Span<bool> rs = stackalloc bool[129]; bool inc = true;
			if (x != b.head?.from) // inclusive range omitted, use default
				Set.RI.CopyTo(rs);
			foreach (var r in b) {
				inc &= x == (x = r.from); // before ^
				if (gram[x] == '\\')
					foreach (var c in MetaStr.Unesc(gram, x, r.to, true))
						rs[c] = inc;
				else
					for (char c = gram[x], cc = gram[r.to - 1]; c <= cc; c++)
						rs[c] = inc; // R-R
				x = r.to;
			}
			var s = new char[129];
			int n = 0;
			for (char y = '\0'; y <= 128; y++)
				if (rs[y]) s[n++] = y;
			if (n == 0)
				throw new($"No byte in {k}.{part} :{meta.ler.Lexs(b.from, b.to)}");
			es[en++] = (ReadOnlyMemory<char>)s.AsMemory(0, n);
			++x; // range ]
		}
		else // single byte
			es[en++] = Set.ONE[gram[x++]];
		// byte dup +
		if (x < b.to)
			es[en++] = Range.All;
	}

	static void Dump<K>(SyntStr top, LexGram<K> gram)
	{
		top.Dump();
		using var env = EnvWriter.Use();
		env.WriteLine("gram");
		foreach (var prod in gram.prods) {
			env.Write($".prod({prod.key})");
			foreach (var part in prod) {
				env.Write(part.redo ? "\n  .redo" : "\n  .part");
				foreach (var alt in part) {
					env.Write("[");
					foreach (var elem in alt) {
						if (elem.inc.Length > 0) env.Write($"[{elem.inc}]");
						else env.Write($"\"{elem.str}\"");
						if (elem.dup) env.Write(",..");
						if (elem != alt[^1]) env.Write(",");
					}
					env.Write(alt.loop ? "].loop" : "]");
				}
			}
			env.WriteLine();
		}
	}
}
