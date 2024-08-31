//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace qutum.syntax;

using K = Lex;
using Set = CharSet;

// lexemes
public enum Lex
{
	// 0 for end of read
	// kinds
	LITERAL = 1,
	POST,  // postfix operators
	PRE,   // prefix operators
	BIN1,
	BIN2,  // logical binary operators
	BIN3,  // comparison binary operators
	BIN43, // arithmetic binary operators, also prefix
	BIN46, // arithmetic binary operators
	BIN53, // bitwise binary operators
	BIN56, // bitwise binary operators
	BIN6,

	// singles
	BIND = 16                     /**/, LP, LSB, LCB,
	RUN = (Right | LCB & 255) + 1 /**/, RP, RSB, RCB,
	EOL = (Blank | RCB & 255) + 1, IND, DED, INDR, DEDR,
	SP, COMM, COMMB, PATH, NUM,

	// inside kinds
	// literal
	STR = Other | LITERAL << 8 | 1, STRB, NAME, HEX, INT, FLOAT,
	// postfix
	RUNP = Right | POST << 8 | 1,
	// logical
	AND = Bin | BIN2 << 8 | 1, OR, XOR, NOT = Other | PRE << 8 | 2,
	// comparison
	EQ = Bin | BIN3 << 8 | 1, UEQ, LEQ, GEQ, LT, GT,
	// arithmetic
	ADD = Bin | BIN43 << 8 | 1, SUB,
	MUL = Bin | BIN46 << 8 | 1, DIV, MOD, DIVF, MODF,
	// bitwise
	ANDB = Bin | BIN53 << 8 | 1, ORB, XORB, NOTB = Other | PRE << 8 | 1,
	SHL = Bin | BIN56 << 8 | 1, SHR,

	// groups
	Other  /**/= 0x800_0000, // other group 8<<24
	Right  /**/= 0x801_0000, // right-side group
	Bin    /**/= 0x802_0000, // binary group
	Blank  /**/= 0x880_0000, // blank group
}

// lexic parser
public sealed class Lexier : Lexier<K>, Lexer<K, Lexi<K>>
{
	/*	EXC   = ! == not
		QUO   = " == string
		HASH  = #
		DOL   = $
		AMP   = & == and
		APO   = ' == string?
		COM   = ,
		DOT   = . == run
		COL   = :
		SCOL  = ;
		EQ    = = == bind
		QUE   = \?
		AT    = @
		BSL   = \\ == byte block
		HAT   = ^
		BAPO  = ` == path
		VER   = \| == or
		TIL   = ~
	*/
	static readonly LexGram<K> Grammar = new LexGram<K>()
		.k(K.BIND).w["="]

		.k(K.AND).w["&"].k(K.OR).w["|"].k(K.XOR).w["+|"].k(K.NOT).w["!"]

		.k(K.EQ).w["=="].k(K.UEQ).w["/="]
		.k(K.LEQ).w["<="].k(K.GEQ).w[">="].k(K.LT).w["<"].k(K.GT).w[">"]

		.k(K.ADD).w["+"].k(K.SUB).w["-"]
		.k(K.MUL).w["*"].k(K.DIV).w["/"].k(K.MOD).w["%"]
		.k(K.DIVF).w["//"].k(K.MODF).w["%%"]

		.k(K.ANDB).w["&&"].k(K.ORB).w["||"].k(K.XORB).w["++"].k(K.NOTB).w["--"]
		.k(K.SHL).w["<<"].k(K.SHR).w[">>"]

		.k(K.LP).w["("].k(K.RP).w[")"]
		.k(K.LSB).w["["].k(K.RSB).w["]"]
		.k(K.LCB).w["{"].k(K.RCB).w["}"]

		.k(K.EOL).w["\n"]["\r\n"]
		.k(K.SP).w[" \t".Mem()] // [\s\t]  |+\s+|+\t+
				.w[[]][" ", ..].loop["\t", ..].loop

		.k(K.COMM).w["##"] // ##  |[\A^\n]+
					.w[[]][Set.All.Exc("\n"), ..]
		.k(K.COMMB).w["\\", .., "#"] // \\+#  +#\\+|+#|+[\A^#]+
					.w["#", "\\", ..].loop["#"].loop[Set.All.Exc("#"), ..].loop

		.k(K.STRB).w["\\", .., "\""] // \\+"  +"\\+|+"|+[\A^"]+
					.w["\"", "\\", ..].loop["\""].loop[Set.All.Exc("\""), ..].loop
		.k(K.STR).w["\""] // "  *"|\n|\r\n|+[\L^"\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["\"\n".Mem()]["\r\n"]
					[Set.Line.Exc("\"\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(K.PATH).w["`"][".`"] // .?`  *`|\n|\r\n|+.|+[\L^.`\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["`\n".Mem()]["\r\n"]["."].loop
					[Set.Line.Exc(".`\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(K.NAME).w[Set.Alpha.Inc("_")][Set.Alpha.Inc("_"), Set.Word, ..] // [\a_]\w*
		.k(K.RUN).w["."] // .|.[\a_]\w*
						[".", Set.Alpha.Inc("_")][".", Set.Alpha.Inc("_"), Set.Word, ..]

		.k(K.HEX).w["0x"]["0X"] // 0[xX]  _*\x  |+_*\x+
				.w[Set.Hex]["_", .., Set.Hex]
				.w[[]][Set.Hex, ..].loop["_", .., Set.Hex, ..].loop
		.k(K.NUM) // 0|[1-9]  |+_*\d+  |.\d+  |+_+\d+  |[eE][\+\-]?\d+  |[fF]
				.w["0"][Set.Dec.Exc("0")]
				.w[[]][Set.Dec, ..].loop["_", .., Set.Dec, ..].loop
				.w[[]][".", Set.Dec, ..]
				.w[[]]["_", .., Set.Dec, ..].loop
				.w[[]]["eE".Mem(), Set.Dec, ..]["eE".Mem(), "+-".Mem(), Set.Dec, ..]
				.w[[]]["fF".Mem()]
	;

	// key ordinal is single or kind value, useful for syntax parser
	public static ushort Ordin(Lexier ler)
		=> (byte)((int)ler.lexs[ler.loc].key >> ((int)ler.lexs[ler.loc].key >> 24));

	public static bool IsGroup(K key, K aim) => (int)(key & aim) << 8 != 0;
	public static bool IsKind(K key, K aim) => (byte)((int)key >> ((int)key >> 24)) == (byte)aim;
	public override bool Is(K key, K aim) =>
		(byte)aim == 0 ? IsGroup(key, aim) : (int)aim <= 15 ? IsKind(key, aim) : key == aim;

	// check each key distinct from others, otherwise throw exception
	public static void Distinct(IEnumerable<K> keys)
	{
		int kinds = 0;
		StrMaker err = new();
		foreach (var k in keys)
			if ((byte)k == 0)
				_ = err - ' ' + k; // denied group
		foreach (var k in keys)
			if ((int)k <= 15 && (kinds ^ (1 << (int)k)) != (kinds |= 1 << (int)k))
				_ = err - ' ' + k; // duplicate kind
		foreach (var k in keys)
			if ((int)k > 15 && (kinds & (1 << (byte)((int)k >> 8))) != 0)
				_ = err - ' ' + k; // key inside kinds
		if (err.Size > 0)
			throw new(err);
	}

	public Lexier() : base(Grammar) { }

	byte[] bs = new byte[4096]; // buffer used by some lexi
	int bz;
	int ni, nf, ne; // end of each number wad
	int indb; // indent byte, unknown: -1
	int indz; // inds size
	int[] inds = new int[100]; // {0, indent column...}
	int ind, indf, indt; // indent column 0 based, read from loc to excluded loc
	bool crlf; // \r\n found
	readonly List<string> path = [];
	public bool eor = true; // insert eol at eor
	public bool allValue = false; // set all lexis value
	public bool allBlank = false; // keep spaces without indent and offset, comments and empty lines

	public override void Clear()
	{
		base.Clear();
		indb = ind = -1; indz = indf = indt = 0; inds[0] = 0;
		crlf = false; path.Clear();
	}

	int Read(int f, int to, int x)
	{
		var n = x + to - f;
		if (bs.Length < n)
			Array.Resize(ref bs, n + 4095 & ~4095);
		read.Lexs(f, to, bs.AsSpan(x));
		return n;
	}

	public const int IndLoc = 2, IndrLoc = 6;

	void Indent()
	{
		if (ind < 0)
			return;
		int i = 1, c = ind;
		if (indz > 0) {
			i = Array.BinarySearch<int>(inds, 0, indz + 1, ind + IndLoc); // min indent as offset
			i ^= i >> 31;
			c = ind - inds[i - 1];
			if (i <= indz) { // drop these indents
				for (var x = indz; x >= i; indz = --x)
					if (x > i || c < IndrLoc)
						base.Lexi(inds[x] - inds[x - 1] < IndrLoc ? K.DED : K.DEDR,
							indf, indf, inds[x]);
					else { // still INDR
						Error(K.INDR, indf, indt, "indent expected same as upper lines");
						goto Done;
					}
			}
		}
		if (c >= IndLoc) {
			base.Lexi(c < IndrLoc ? K.IND : K.INDR, indf, indt, ind);
			indz = i;
			if (indz >= inds.Length) Array.Resize(ref inds, inds.Length << 1);
			inds[i] = ind;
		}
	Done: ind = -1;
	}

	protected override void Eor(int bz)
	{
		var end = true;
		if (eor)
			Wad(K.EOL, 1, ref end, bz, bz);
		ind = 0; indf = indt = bz; Indent();
	}

	protected override void Lexi(K key, int f, int to, object value)
	{
		Indent();
		Lexi<K> p;
		if (size > 0 && (p = lexs[size - 1]).to == f)
			if (key == K.RUN && (IsKind(p.key, K.LITERAL) || IsGroup(p.key, K.Right)))
				key = K.RUNP; // run follows previous lexi densely, high precedence
			else if (IsKind(key, K.LITERAL) && IsKind(p.key, K.LITERAL))
				Error(key, f, to, "literal can not densely follow literal");
			else if (IsKind(key, K.PRE) && !(IsKind(p.key, K.PRE) || IsGroup(p.key, K.Blank | K.Bin)))
				Error(key, f, to, "prefix operator can densely follow blank and prefix and binary only");
		base.Lexi(key, f, to, value);
	}

	protected override void WadErr(K key, int wad, bool end, int b, int f, int to)
	{
		if (key == K.PATH)
			key = K.NAME;
		base.WadErr(key, wad, end, b, f, to);
	}

	protected override void Wad(K key, int wad, ref bool end, int f, int to)
	{
		object d = null;
		if (from < 0) {
			from = f; bz = 0; path.Clear();
		}
		switch (key) {

		case K.EOL:
			if (!crlf && to == f + 2) { // \r\n found
				Error(key, f, to, @"use LF \n eol instead of CRLF \r\n");
				crlf = true;
			}
			if (allBlank
				|| size > 0 && lexs[size - 1].key != K.EOL) // no EOL for empty line
				base.Lexi(key, from, to, null);
			ind = 0; indf = indt = to;
			goto End;

		case K.SP:
			if (wad == 1)
				bs[0] = (byte)(f < 1 || read.Lex(f - 1) == '\n' ? 1 : 0); // maybe line start
			if (bs[0] != 0)
				if (indb < 0)
					indb = read.Lex(f); // first line start, save the indent byte
				else if (f < to && read.Lex(f) != indb) { // mix \s and \t
					bs[0] = 0; // as not line start and indent unchanged
					Error(key, f, to, "do not mix tabs and spaces for indent"); // TODO omit this before COMM
				}
			if (!end)
				return;
			if (bs[0] != 0) {
				ind = to - from << (indb == '\t' ? 2 : 0); // 4 column each \t
				indf = from; indt = to;
			}
			else if (allBlank)
				Lexi(key, from, to, null);
			goto End; // lexis already made

		case K.COMM:
			if (!allBlank)
				goto End;
			break;

		case K.COMMB:
			if (wad == 1) {
				bz = to; // begin wad loc
				return;
			}
			if (to - f != bz - from || read.Lex(f) != '#') // check end wad size
				return;
			end = true;
			if (!allBlank)
				goto End;
			key = K.COMM; d = K.COMMB; // as COMM
			break;

		case K.STRB:
			if (wad == 1) {
				bz = to; // begin wad loc
				return;
			}
			if (to - f != bz - from || read.Lex(f) != '"') // check end wad size
				return;
			end = true; bz = Read(bz, f, 0);
			break;

		case K.STR:
			if (wad == 1)
				return;
			if (end) {
				if (read.Lex(to - 1) == '\n') {
					Error(key, f, to, "eol unexpected");
					BackByte(to = f); // next lexi will be eol
				}
				break;
			}
			Read(f, to, bz); Unesc(f, to);
			return; // inside string

		case K.HEX:
			if (!end)
				return;
			bz = Read(from, to, 0);
			key = K.INT; d = Hex(); // as INT
			break;

		case K.NUM:
			if (wad == 2) ni = to - from; // end of integer wad
			else if (wad == 4) nf = to - from; // end of fraction wad
			else if (wad == 5) ne = to - from; // end of exponent wad
			if (!end)
				return;
			bz = Read(from, to, 0);
			d = Num(ref key);
			break;

		case K.NAME:
		case K.RUN:
			f = key == K.NAME ? from : from + 1;
			if (to - f > 40) {
				Error(key, f, to, "too long");
				to = f + 40;
			}
			bz = Read(f, to, 0);
			break;

		case K.PATH:
			if (wad == 1)
				return;
			var split = end || read.Lex(f) == '.';
			if (split) {
				if (bz > 40) {
					Error(K.NAME, to - 1, to - 1, "too long");
					bz = 40;
				}
				path.Add(Encoding.UTF8.GetString(bs, 0, bz));
				bz = 0;
			}
			if (end) {
				if (read.Lex(to - 1) == '\n') {
					Error(K.NAME, f, to, "eol unexpected");
					BackByte(to = f); // next lexi will be eol
				}
				key = read.Lex(from) != '.' ? K.NAME : K.RUN;
				d = path.ToArray();
				break;
			}
			if (!split) {
				Read(f, to, bz); Unesc(f, to);
			}
			return; // inside path

		default:
			if (allValue)
				bz = Read(from, to, 0);
			break;
		}
		if (!end)
			return;
		Lexi(key, from, to, d ?? (bz > 0 ? Encoding.UTF8.GetString(bs, 0, bz) : null));
	End: // lexi already made
		from = -1;
	}

	void Unesc(int f, int to)
	{
		if (bs[bz] != '\\')
			bz += to - f;
		else // unescape
			switch (bs[bz + 1]) {
			case (byte)'0': bs[bz++] = (byte)'\0'; break;
			case (byte)'t': bs[bz++] = (byte)'\t'; break;
			case (byte)'n': bs[bz++] = (byte)'\n'; break;
			case (byte)'r': bs[bz++] = (byte)'\r'; break;
			case (byte)'x': bs[bz++] = (byte)(Hex(bz + 1) << 4 | Hex(bz + 2)); break;
			case (byte)'u':
				Span<char> u = [
					(char)(Hex(bz + 2) << 12 | Hex(bz + 3) << 8 | Hex(bz + 4) << 4 | Hex(bz + 5)) ];
				bz += Encoding.UTF8.GetBytes(u, bs.AsSpan(bz)); break;
			default: bs[bz++] = bs[bz]; break;
			}
	}

	int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);

	object Hex()
	{
		uint v = 0;
		for (int x = 2; x < bz; x++)
			if (bs[x] != '_')
				if (v < 0x1000_0000)
					v = v << 4 | (uint)Hex(x);
				else {
					Error(K.INT, from, from + bz, "hexadecimal out of range");
					return 0;
				}
		return v;
	}

	object Num(ref K key)
	{
		uint v = 0; int x = 0, dot = 0, e = 0;
		if (ni == bz) {
			key = K.INT; // as INT
			for (; x < ni; x++)
				if (bs[x] != '_')
					if (v < 214748364 || v == 214748364 && bs[x] <= '8')
						v = v * 10 + bs[x] - '0';
					else {
						Error(key, from, from + ni, "integer out of range");
						return 0;
					}
			return v;
		}
		key = K.FLOAT; // as FLOAT
		for (; x < ni; x++)
			if (bs[x] != '_')
				if (v <= 9999_9999) v = v * 10 + bs[x] - '0';
				else {
					dot = ni - x;
					break;
				}
		if (ni < nf)
			for (x = ni + 1; x < nf; x++)
				if (bs[x] != '_')
					if (v <= 9999_9999) {
						v = v * 10 + bs[x] - '0'; dot--;
					}
					else
						break;
		if (v == 0)
			return 0f;
		if (nf < ne) {
			bool neg = bs[nf + 1] == '-';
			for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; ++x < ne;)
				e = e * 10 + bs[x] - '0';
			if (neg)
				e = -e;
		}
		e += dot;
		if (e <= 0)
			return e < -54 ? 0f : e < -37 ? v / Exps[37] / Exps[-e - 37] : v / Exps[-e];
		float w = v * Exps[e < 39 ? e : 39];
		if (float.IsInfinity(w)) {
			Error(key, from, from + bz, "float out of range");
			return 0f;
		}
		return w;
	}

	static readonly float[] Exps = [
		1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
		1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
		1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
		1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
	];
}
