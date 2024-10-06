//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
#pragma warning disable IDE0078 // Use pattern matching
using qutum.parser.earley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser.meta;

sealed class MetaStr(string read) : LerStr(read)
{
	public override bool Is(char aim) => Is(read[loc], aim);

	public override bool Is(int loc, char aim) => Is(read[loc], aim);

	// for meta grammar
	public override bool Is(char k, char aim) =>
		aim switch {
			'S' => k is ' ' or '\t',        // space
			'X' => k < 127 && CharSet.X[k], // hexadecimal
			'W' => k < 127 && CharSet.W[k], // word
			'O' => k < 127 && CharSet.O[k], // operator
			'G' => k < 127 && CharSet.G[k], // grammar operator
			'E' => k > ' ' && k < 127,      // escape
			'I' => k < 127 && CharSet.I[k], // single range
			'R' => k < 127 && CharSet.I[k] && k != '-' && k != '^', // range
			'Q' => k is '?' or '*' or '+',                          // quantifier
			'H' => k >= ' ' && k < 127 && k != '=' && k != '|',     // hint
			'V' => k is >= ' ' or '\t',                             // comment
			_ => k == aim,
		};

	// for general grammar
	internal static string Unesc(string s, Jov j, bool lexer = false)
	{
		if (s[j.on] != '\\')
			return s[j.range];
		char c = s[++j.on];
		return c switch {
			's' => " ",
			't' => "\t",
			'n' => "\n",
			'r' => "\r",
			'0' => "\0",
			_ => (lexer ? c : '\0') switch {
				'A' => CharSet.ALL,
				'l' => CharSet.LINE,
				'd' => CharSet.DEC,
				'x' => CharSet.HEX,
				'a' => CharSet.ALPHA,
				'w' => CharSet.WORD,
				'u' => "\x80",
				_ => c < 129 ? CharSet.BYTE[c] : c.ToString(),
			}
		};
	}
}

public static class MetaLex
{
	// gram
	// \ prod
	//   \ key
	//   \ wad1
	//     \ byte: single/range.../esc (dup) ...
	//     \ alt1 ...
	//       \ byte: single/range.../esc (dup) ...
	//   \ wad ...
	//     \ redo
	//     \ loop
	//     \ byte: single/range.../esc (dup) ...
	//     \ alt ...
	//       \ loop
	//       \ byte: single/range.../esc (dup) ...
	// \ prod ...
	static readonly EarleyStr meta = new("""
		gram  = eol* prod prods* eol*
		prods = eol+ prod
		prod  = key S*\=S* wad1 wad* =+
		key   = W+ =+
		wad1 = byte+ alt1* =+
		alt1  = \| S* byte+ =+
		wad  = S+ redo? loop? byte+ alt* =+
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
		=> Gram(gram, Lexier<K>.Keys_, dump);

	public static LexGram<K> Gram<K>(string gram, Func<string, IEnumerable<K>> Keys, bool dump = false)
		 where K : struct
	{
		using var read = new MetaStr(gram);
		var top = meta.Begin(read).Parse();
		if (top.err != 0) {
			using var read2 = new MetaStr(gram);
			var dum = meta.dump; meta.dump = 3;
			meta.Begin(read2).Parse().Dump(); meta.dump = dum;
			var e = new Exception("lexic grammar error"); e.Data["err"] = top;
			throw e;
		}
		LexGram<K> g = new();
		var es = new object[Lexier<K>.AltByteN << 1];
		// build prod
		foreach (var prod in top) {
			var k = Keys(meta.ler.Lexs(prod.head.j)).Single();
			g.k(k);
			// build wad
			foreach (var (w, wad) in prod.Skip(1).Each(1)) {
				if (w.head.name != "redo") // no backward cross wads
					_ = g.w;
				else if (gram[w.head.j.on] == '|') // empty alt
					_ = g.w[[]];
				else // shift byte and redo wad like the begin
					_ = g.redo;
				// build alt
				foreach (var (a, alt) in w.Where(t => t.name.StartsWith("alt")).Prepend(w).Each(1)) {
					// build elems
					var bytes = a.Where(t => t.name == "byte");
					var bz = bytes.Count();
					if (bz > Lexier<K>.AltByteN)
						throw new($"{k}.{wad}.{alt} exceeds {Lexier<K>.AltByteN} bytes :"
							+ meta.ler.Lexs(a.j));
					var ez = 0;
					foreach (var (b, bx) in bytes.Each())
						Byte(gram, k, wad, alt, b, es, ref ez);
					_ = g[es[0..ez]];
					if (a.head.name == "loop" || a.head.next?.name == "loop")
						_ = g.loop;
				};
			}
		}
		if (dump) Dump(top, g);
		return g;
	}

	static void Byte<K>(string gram, K k, int wad, int alt, EsynStr b, object[] es, ref int ez)
	{
		var x = b.j.on;
		if (gram[x] == '\\') { // escape bytes
			es[ez++] = MetaStr.Unesc(gram, (x, b.j.via), true).One();
			x += 2;
		}
		// build range
		else if (gram[x] == '[') {
			++x;
			Span<bool> rs = stackalloc bool[129]; bool inc = true;
			if (x != b.head?.j.on) // inclusive range omitted, use default
				CharSet.RI.CopyTo(rs);
			foreach (var r in b) {
				inc &= x == (x = r.j.on); // before ^
				if (gram[x] == '\\')
					foreach (var c in MetaStr.Unesc(gram, (x, r.j.via), true))
						rs[c] = inc;
				else
					for (char c = gram[x], cc = gram[r.j.via - 1]; c <= cc; c++)
						rs[c] = inc; // R-R
				x = r.j.via;
			}
			var s = new char[129];
			int n = 0;
			for (char y = '\0'; y <= 128; y++)
				if (rs[y]) s[n++] = y;
			if (n == 0)
				throw new($"No byte in {k}.{wad} :{meta.ler.Lexs(b.j)}");
			es[ez++] = (ReadOnlyMemory<char>)s.AsMemory(0, n);
			++x; // range ]
		}
		else // single byte
			es[ez++] = CharSet.BYTE[gram[x++]];
		// byte dup +
		if (x < b.j.via)
			es[ez++] = Range.All;
	}

	static void Dump<K>(EsynStr top, LexGram<K> gram) where K : struct
	{
		using var env = EnvWriter.Use();
		env.WriteLine("meta:");
		top.Dump();
		env.WriteLine("gram:");
		foreach (var prod in gram.prods) {
			env.Write($".k({prod.key})");
			foreach (var wad in prod) {
				env.Write(wad.redo ? "\n  .redo" : "\n  .w");
				foreach (var alt in wad) {
					env.Write("[");
					foreach (var (c, cx) in alt.Each()) {
						if (c.one.Length > 0) env.Write($"[{c.one}]");
						else env.Write($"\"{c.str}\"");
						if (c.dup) env.Write(",..");
						if (cx < alt.Count - 1) env.Write(",");
					}
					env.Write(alt.loop ? "].loop" : "]");
				}
			}
			env.WriteLine();
		}
	}
}
