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

sealed class BootScan : ScanStr
{
	public BootScan(string input) : base(input) { }

	public override bool Is(char key) => Is(input[loc], key);

	public override bool Is(int loc, char key) => Is(input[loc], key);

	// for boot grammar
	public override bool Is(char t, char key) =>
		key switch {
			'S' => t is ' ' or '\t',   // space
			'W' => t < 127 && W[t],    // word
			'X' => t < 127 && X[t],    // hexadecimal
			'O' => t < 127 && O[t],    // operator
			'G' => t < 127 && G[t],    // operator for grammar
			'E' => t > ' ' && t < 127, // escape
			'I' => t < 127 && I[t],    // single 
			'R' => t < 127 && I[t] && t != '-' && t != '^', // range
			'Q' => t is '?' or '*' or '+',                  // quantifier
			'H' => t >= ' ' && t < 127 && t != '=' && t != '|', // hint
			'V' => t is >= ' ' or '\t',                         // comment
			_ => t == key,
		};

	internal static bool[] L = new bool[129], D = new bool[129], X = new bool[129],
							A = new bool[129], W = new bool[129], O = new bool[129],
							G = new bool[129], I = new bool[129],
							RI = new bool[129]; // default inclusive range
	internal static string All, Line, Dec, Hex, Alpha, Word, Op;
	internal static string[] One; // one bytes

	static BootScan()
	{
		foreach (var t in "!\"#$%&'()*+,-./:;<=>?@[\\]^`{|}~") {
			O[t] = true;
			G[t] = t is not ('*' or '+' or '=' or '?' or '\\' or '|');
		}
		for (char t = '\0'; t < 127; t++) {
			L[t] = t is >= ' ' or '\t';
			D[t] = t is >= '0' and <= '9';
			X[t] = (D[t]) || t is >= 'a' and <= 'f' or >= 'A' and <= 'F';
			A[t] = t is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
			W[t] = (D[t] || A[t]) || t == '_';
			I[t] = (W[t] || G[t]) && t != '[' && t != ']' || t == '=';
			RI[t] = t is >= ' ' or '\t' or '\n' or '\r';
		}
		L[128] = true;
		var all = Enumerable.Range(0, 129);
		All = new string(all.Select(b => (char)b).ToArray());
		Line = new string(all.Where(b => L[b]).Select(b => (char)b).ToArray());
		Dec = new string(all.Where(b => D[b]).Select(b => (char)b).ToArray());
		Hex = new string(all.Where(b => X[b]).Select(b => (char)b).ToArray());
		Alpha = new string(all.Where(b => A[b]).Select(b => (char)b).ToArray());
		Word = new string(all.Where(b => W[b]).Select(b => (char)b).ToArray());
		Op = new string(all.Where(b => O[b]).Select(b => (char)b).ToArray());
		One = Enumerable.Range(0, 129).Select(b => new string((char)b, 1)).ToArray();
	}

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
				'A' => All,
				'l' => Line,
				'd' => Dec,
				'x' => Hex,
				'a' => Alpha,
				'w' => Word,
				'u' => "\x80",
				_ => c < 129 ? One[c] : c.ToString(),
			}
		};
	}
}

public static class BootLexer
{
	public static readonly ReadOnlyMemory<char> All = BootScan.All.AsMemory(),
		Line = BootScan.Line.AsMemory(), Dec = BootScan.Dec.AsMemory(), Hex = BootScan.Hex.AsMemory(),
		Alpha = BootScan.Alpha.AsMemory(), Word = BootScan.Word.AsMemory(), Op = BootScan.Op.AsMemory();

	public static ReadOnlyMemory<char> Inc(this ReadOnlyMemory<char> inc, ReadOnlyMemory<char> more)
		=> inc.AsEnum().Union(more.AsEnum()).ToArray().AsMemory();
	public static ReadOnlyMemory<char> Inc(this ReadOnlyMemory<char> inc, string more)
		=> Inc(inc, more.AsMemory());
	public static ReadOnlyMemory<char> Exc(this ReadOnlyMemory<char> inc, ReadOnlyMemory<char> exc)
		=> inc.AsEnum().Except(exc.AsEnum()).ToArray().AsMemory();
	public static ReadOnlyMemory<char> Exc(this ReadOnlyMemory<char> inc, string exc)
		=> Exc(inc, exc.AsMemory());

	// gram
	// \ prod
	//   \ key
	//   \ part1
	//     \ byte: single or range... or esc ... (rep)
	//     \ alt1 ...
	//       \ byte: single or range... or esc ... (rep)
	//   \ part ...
	//     \ skip
	//     \ loop
	//     \ byte: single or range... or esc ... (rep)
	//     \ alt ...
	//       \ loop
	//       \ byte: single or range... or esc ... (rep)
	// \ prod ...
	static readonly ParserStr boot = new("""
		gram  = eol* prod prods* eol*
		prods = eol+ prod
		prod  = key S*\=S* part1 part* =+
		key   = W+ =+
		part1 = byte+ alt1* =+
		alt1  = \| S* byte+ =+
		part  = S+ skip? loop? byte+ alt* =+
		alt   = \| S* loop? byte+ =+
		skip  = \* | \| S* =+
		loop  = \+ =+
		byte  = I \+? | [range* ^? range*] \+? | \\E \+? =+
		range = R | R-R | \\E =+
		eol   = S* comm? \r?\n S*
		comm  = \=\= V*
		""") {
		greedy = false, tree = false, dump = 0
	};

	public static LexerGram<K> Gram<K>(string gram, bool dump = false) where K : struct
		=> Gram(gram, Lexer<K>.Keyz, dump);

	public static LexerGram<K> Gram<K>(string gram, Func<string, IEnumerable<K>> Keys, bool dump = false)
	{
		using var bscan = new BootScan(gram);
		var top = boot.Load(bscan).Parse();
		if (top.err != 0) {
			using var bscan2 = new BootScan(gram);
			var dum = boot.dump; boot.dump = 3;
			boot.Load(bscan2).Parse().Dump(); boot.dump = dum;
			var e = new Exception(); e.Data["err"] = top;
			throw e;
		}
		LexerGram<K> g = new();
		var es = new object[LexerGram<K>.AltByteN << 1];
		// build prod
		foreach (var prod in top) {
			var k = Keys(boot.scan.Tokens(prod.head.from, prod.head.to)).Single();
			g.prod(k);
			// build part
			prod.Skip(1).Each((p, part) => {
				++part;
				if (p.head.name != "skip") // no backward cross parts
					_ = g.part;
				else if (gram[p.head.from] == '|') // empty alt
					_ = g.part[""];
				else // shift byte and retry part like the start
					_ = g.skip;
				// build alt
				p.Where(t => t.name.StartsWith("alt")).Prepend(p).Each((a, alt) => {
					++alt;
					// build elems
					var bytes = a.Where(t => t.name == "byte");
					var bn = bytes.Count();
					if (bn > LexerGram<K>.AltByteN)
						throw new Exception($"{k}.{part}.{alt} exceeds {LexerGram<K>.AltByteN} bytes :"
							+ boot.scan.Tokens(a.from, a.to));
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

	static void Byte<K>(string gram, K k, int part, int alt, TreeStr b, object[] es, ref int en)
	{
		var x = b.from;
		if (gram[x] == '\\') { // escape bytes
			es[en++] = BootScan.Unesc(gram, x, b.to, true).AsMemory();
			x += 2;
		}
		// build range
		else if (gram[x] == '[') {
			++x;
			Span<bool> rs = stackalloc bool[129]; bool inc = true;
			if (x != b.head?.from) // inclusive range omitted, use default
				BootScan.RI.CopyTo(rs);
			foreach (var r in b) {
				inc &= x == (x = r.from); // before ^
				if (gram[x] == '\\')
					foreach (var c in BootScan.Unesc(gram, x, r.to, true))
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
				throw new Exception($"No byte in {k}.{part} :{boot.scan.Tokens(b.from, b.to)}");
			es[en++] = (ReadOnlyMemory<char>)s.AsMemory(0, n);
			++x; // range ]
		}
		else // single byte
			es[en++] = BootScan.One[gram[x++]];
		// byte loop +
		if (x < b.to)
			es[en++] = Range.All;
	}

	static void Dump<K>(TreeStr top, LexerGram<K> gram)
	{
		top.Dump();
		using var env = EnvWriter.Use();
		env.WriteLine("gram");
		foreach (var prod in gram.prods) {
			env.Write($".prod({prod.key})");
			foreach (var part in prod) {
				env.Write(part.skip ? "\n  .skip" : "\n  .part");
				foreach (var alt in part) {
					env.Write("[");
					foreach (var elem in alt) {
						if (elem.inc.Length > 0) env.Write($"[{elem.inc}]");
						else env.Write($"\"{elem.str}\"");
						if (elem.rep) env.Write(",..");
						if (elem != alt[^1]) env.Write(",");
					}
					env.Write(alt.loop ? "].loop" : "]");
				}
			}
			env.WriteLine();
		}
	}
}
