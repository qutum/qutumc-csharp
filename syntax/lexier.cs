//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.syntax;

using Kord = char;
using L = Lex;
using Set = CharSet;

/*	EXC		!	not
	QUO		"	string
	HASH	#
	DOL		$
	AMP		&	and
	APO		'	string?
	COM		,	input
	DOT		.	run
	COL		:
	SCOL	;
	EQ		=	bind
	QUE		?
	AT		@
	BSL		\	byte block
	HAT		^
	BAPO	`	path
	VER		|	or
	TIL		~
*/

// lexemes
public enum Lex : int
{
	// 0 for end of read
	// kinds 0xff<<8
	LITERAL = 1,
	BIN1,
	BIN2,   // logical binary operators
	BIN3,   // comparison binary operators
	BIN43,  // arithmetic binary operators
	BIN46,	// arithmetic binary operators
	BIN53,	// bitwise binary operators
	BIN56,	// bitwise binary operators
	BIN6,
	PRE,   // prefix operators
	POST,  // postfix operators

	// singles
	INP = 0x10, BIND,
	LP = (Left | BIND & 255) + 1, LSB, LCB,
	RP = (Right | LCB & 255) + 1, RSB, RCB, RUN,
	EOL = (Blank | RUN & 255) + 1, IND, DED, INDR, DEDR,
	SP, COMM, COMMB, PATH, NUM,

	// inside kinds
	// literal
	STR = Kind | LITERAL << 8 | 1, STRB, NAME, HEX, INT, FLOAT,
	// logical
	AND = Bin | BIN2 << 8 | 1, OR, XOR,
	// comparison
	EQ = Bin | BIN3 << 8 | 1, UEQ, LEQ, GEQ, LT, GT,
	// arithmetic
	ADD = Bin | BIN43 << 8 | 1, SUB,
	MUL = Bin | BIN46 << 8 | 1, DIV, MOD, DIVF, MODF,
	// bitwise
	ANDB = Bin | BIN53 << 8 | 1, ORB, XORB,
	SHL = Bin | BIN56 << 8 | 1, SHR,
	// prefix: logical, arithmetic, bitwise
	NOT = Left | Kind | PRE << 8 | 1, POSI, NEGA, NOTB,
	// postfix
	RUNP = Right | Kind | POST << 8 | 1,

	// groups 0xff<<16
	Kind   /**/= 0x800_0000, // kind group 8<<24
	Blank  /**/= 0x_01_0000, // blank group
	Left   /**/= 0x_02_0000, // left-side group
	Right  /**/= 0x_04_0000, // right-side group
	Bin    /**/= 0x806_0000, // binary kind group
}

// lexic parser
public sealed class Lexier : Lexier<L>, Lexer<L, Lexi<L>>
{
	static readonly LexGram<L> Grammar = new LexGram<L>()
		.k(L.INP).w[","].k(L.BIND).w["="]

		.k(L.AND).w["&"].k(L.OR).w["|"].k(L.XOR).w["!="].k(L.NOT).w["!"]

		.k(L.EQ).w["=="].k(L.UEQ).w["/="]
		.k(L.LEQ).w["<="].k(L.GEQ).w[">="].k(L.LT).w["<"].k(L.GT).w[">"]

		.k(L.ADD).w["+"].k(L.SUB).w["-"]
		.k(L.MUL).w["*"].k(L.DIV).w["/"].k(L.MOD).w["%"]
		.k(L.DIVF).w["//"].k(L.MODF).w["%%"]

		.k(L.ANDB).w["&&"].k(L.ORB).w["||"].k(L.XORB).w["++"].k(L.NOTB).w["--"]
		.k(L.SHL).w["<<"].k(L.SHR).w[">>"]

		.k(L.LP).w["("].k(L.RP).w[")"]
		.k(L.LSB).w["["].k(L.RSB).w["]"]
		.k(L.LCB).w["{"].k(L.RCB).w["}"]

		.k(L.EOL).w["\n"]["\r\n"]
		.k(L.SP).w[" \t".Mem()] // [\s\t]  |+\s+|+\t+
				.w[[]][" ", ..].loop["\t", ..].loop

		.k(L.COMM).w["##"] // ##  |[\A^\n]+
					.w[[]][Set.All.Exc("\n"), ..]
		.k(L.COMMB).w["\\", .., "#"] // \\+#  +#\\+|+#|+[\A^#]+
					.w["#", "\\", ..].loop["#"].loop[Set.All.Exc("#"), ..].loop

		.k(L.STRB).w["\\", .., "\""] // \\+"  +"\\+|+"|+[\A^"]+
					.w["\"", "\\", ..].loop["\""].loop[Set.All.Exc("\""), ..].loop
		.k(L.STR).w["\""] // "  *"|\n|\r\n|+[\L^"\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["\"\n".Mem()]["\r\n"]
					[Set.Line.Exc("\"\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.PATH).w["`"][".`"] // .?`  *`|\n|\r\n|+.|+[\L^.`\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["`\n".Mem()]["\r\n"]["."].loop
					[Set.Line.Exc(".`\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.NAME).w[Set.Alpha.Inc("_")][Set.Alpha.Inc("_"), Set.Word, ..] // [\a_]\w*
		.k(L.RUN).w["."] // .|.[\a_]\w*
						[".", Set.Alpha.Inc("_")][".", Set.Alpha.Inc("_"), Set.Word, ..]

		.k(L.HEX).w["0x"]["0X"] // 0[xX]  _*\x  |+_*\x+
				.w[Set.Hex]["_", .., Set.Hex]
				.w[[]][Set.Hex, ..].loop["_", .., Set.Hex, ..].loop
		.k(L.NUM) // 0|[1-9]  |+_*\d+  |.\d+  |+_+\d+  |[eE][\+\-]?\d+  |[fF]
				.w["0"][Set.Dec.Exc("0")]
				.w[[]][Set.Dec, ..].loop["_", .., Set.Dec, ..].loop
				.w[[]][".", Set.Dec, ..]
				.w[[]]["_", .., Set.Dec, ..].loop
				.w[[]]["eE".Mem(), Set.Dec, ..]["eE".Mem(), "+-".Mem(), Set.Dec, ..]
				.w[[]]["fF".Mem()]
	;

	// key ordinal is single or kind value, useful for syntax parser
	public static Kord Ordin(L key) => (Kord)(byte)((int)key >> ((int)key >> 24));
	public static Kord Ordin(Lexier ler) => Ordin(ler.lexs[ler.loc].key);
	public static L Ordin(Kord o)
	{
		if (ordins == null) {
			var s = new L[256];
			foreach (var k in Enum.GetValues<L>())
				if ((ushort)k >> 8 == 0)
					s[Ordin(k)] = k;
			s[default] = default;
			ordins = s;
		}
		return ordins[o];
	}
	private static L[] ordins;

	public static bool InGroup(L key, L aim) => (int)(key & aim) << 8 != 0;
	public static bool InKind(L key, L aim) => (byte)((int)key >> ((int)key >> 24)) == (byte)aim;
	public override bool Is(L key, L aim) =>
		(byte)aim == 0 ? InGroup(key, aim) : (int)aim < 0x10 ? InKind(key, aim) : key == aim;

	// check each key distinct from others, otherwise throw exception
	public static void Distinct(IEnumerable<L> keys)
	{
		int kinds = 0;
		StrMaker err = new();
		foreach (var k in keys.Cast<int>())
			if (k > 0 &&
				((byte)k == 0 || // denied group
				k < 0x10 && (kinds ^ (1 << k)) != (kinds |= 1 << k))) // duplicate kind
				_ = err - ' ' + k;
		foreach (var k in keys)
			if ((kinds & (1 << ((ushort)k >> 8))) != 0)
				_ = err - ' ' + k; // key inside kinds
		if (err.Size > 0)
			throw new(err);
	}

	public bool eor = true; // insert eol at eor
	public bool allValue = false; // set all lexis value
	public bool allBlank = false; // keep spaces without indent and offset, comments and empty lines

	public Lexier() : base(Grammar) { }

	public override void Clear()
	{
		base.Clear();
		indb = ind = -1; indz = indf = indt = 0; inds[0] = 0;
		crlf = false; bin = default; path.Clear();
		loc = 0; base.Lexi(L.EOL, 0, 0, null); // eol for lexs[size - 1]
	}

	public const int IndLoc = 2, IndrLoc = 6;
	int indb; // indent byte, unknown: -1
	int indz; // inds size
	int[] inds = new int[100]; // {0, indent column...}
	int ind, indf, indt; // indent column 0 based, read from loc to excluded loc

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
						base.Lexi(inds[x] - inds[x - 1] < IndrLoc ? L.DED : L.DEDR,
							indf, indf, inds[x]);
					else { // still INDR
						base.Error(L.INDR, indf, indt, "indent expected same as upper lines");
						goto Done;
					}
			}
		}
		if (c >= IndLoc) {
			base.Lexi(c < IndrLoc ? L.IND : L.INDR, indf, indt, ind);
			indz = i;
			if (indz >= inds.Length) Array.Resize(ref inds, inds.Length << 1);
			inds[i] = ind;
		}
	Done: ind = -1;
	}

	void BinPre(L key, int f)
	{
		if (bin == default)
			return;
		Indent();
		if (bint == f && !InGroup(key, L.Blank)) // right dense, binary as prefix
			bin = bin switch { L.ADD => L.POSI, L.SUB => L.NEGA, _ => throw new() };
		base.Lexi(bin, binf, bint, null);
		bin = default;
	}

	protected override void Lexi(L key, int f, int to, object value)
	{
		Indent();
		BinPre(key, f);
		if (lexs[size - 1] is Lexi<L> p && p.to == f)
			if (key == L.RUN && (InKind(p.key, L.LITERAL) || InGroup(p.key, L.Right)))
				key = L.RUNP; // run follows previous lexi densely, high precedence
			else if (InKind(key, L.LITERAL) && InKind(p.key, L.LITERAL))
				base.Error(key, f, to, "literal can not densely follow literal");
			else if (InKind(key, L.PRE) && !InGroup(p.key, L.Blank | L.Left))
				base.Error(key, f, to, "prefix operator can densely follow blank or left only");
		base.Lexi(key, f, to, value);
	}
	protected override void Error(L key, int f, int to, object value)
	{
		Indent();
		BinPre(key, f);
		base.Error(key, f, to, value);
	}

	protected override void WadErr(L key, int wad, bool end, int b, int f, int to)
	{
		if (key == L.PATH)
			key = L.NAME;
		base.WadErr(key, wad, end, b, f, to);
	}

	protected override void Eor(int to)
	{
		BinPre(L.EOL, to + 1);
		var end = true;
		if (eor)
			Wad(L.EOL, 1, ref end, to, to);
		ind = 0; indf = indt = to; Indent();
		bin = default;
	}

	int bz; // buffer size
	byte[] bs = new byte[4096]; // long read buffer
	bool crlf; // \r\n found
	bool sol; // line start
	L bin; // binary, maybe prefix
	int binf, bint; // binary, read from loc to excluded loc
	int ni, nf, ne; // end of each number wad
	readonly List<string> path = [];

	protected override void Wad(L key, int wad, ref bool end, int f, int to)
	{
		object d = null;
		if (from < 0) {
			from = f; bz = 0; path.Clear();
		}
		switch (key) {

		case L.EOL:
			if (!crlf && to == f + 2) { // \r\n found
				Error(key, f, to, @"use LF \n eol instead of CRLF \r\n");
				crlf = true;
			}
			if (allBlank || bin != default || lexs[size - 1].key != L.EOL) { // no EOL for empty line
				BinPre(key, from);
				base.Lexi(key, from, to, null);
			}
			ind = 0; indf = indt = to;
			goto End;

		case L.SP:
			if (wad == 1)
				sol = f < 1 || read.Lex(f - 1) == '\n'; // maybe line start
			if (sol)
				if (indb < 0)
					indb = read.Lex(f); // first line start, save the indent byte
				else if (f < to && read.Lex(f) != indb) { // mix \s and \t
					sol = false; // as not line start and indent unchanged
					Error(key, f, to, "do not mix tabs and spaces for indent"); // TODO omit this before COMM
				}
			if (!end)
				return;
			if (sol) {
				ind = to - from << (indb == '\t' ? 2 : 0); // 4 column each \t
				indf = from; indt = to;
			}
			else if (allBlank)
				Lexi(key, from, to, null);
			goto End; // lexis already made

		case L.COMM:
			if (!allBlank)
				goto End;
			break;

		case L.COMMB:
			if (wad == 1) {
				bz = to; // begin wad loc
				return;
			}
			if (to - f != bz - from || read.Lex(f) != '#') // check end wad size
				return;
			end = true;
			if (!allBlank)
				goto End;
			key = L.COMM; d = L.COMMB; // as COMM
			break;

		case L.STRB:
			if (wad == 1) {
				bz = to; // begin wad loc
				return;
			}
			if (to - f != bz - from || read.Lex(f) != '"') // check end wad size
				return;
			end = true; bz = Read(bz, f, 0);
			break;

		case L.STR:
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

		case L.ADD:
		case L.SUB:
			if (lexs[size - 1].to < f || InGroup(lexs[size - 1].key, L.Blank | L.Left)) { // left not dense
				BinPre(key, f); // last binary
				bin = key; binf = f; bint = to; // binary may be prefix
				goto End;
			}
			break;

		case L.HEX:
			if (!end)
				return;
			bz = Read(from, to, 0);
			key = L.INT; d = Hex(); // as INT
			break;

		case L.NUM:
			if (wad == 2) ni = to - from; // end of integer wad
			else if (wad == 4) nf = to - from; // end of fraction wad
			else if (wad == 5) ne = to - from; // end of exponent wad
			if (!end)
				return;
			bz = Read(from, to, 0);
			d = Num(ref key);
			break;

		case L.NAME:
		case L.RUN:
			f = key == L.NAME ? from : from + 1;
			if (to - f > 40) {
				Error(key, f, to, "too long");
				to = f + 40;
			}
			bz = Read(f, to, 0);
			break;

		case L.PATH:
			if (wad == 1)
				return;
			var split = end || read.Lex(f) == '.';
			if (split) {
				if (bz > 40) {
					Error(L.NAME, to - 1, to - 1, "too long");
					bz = 40;
				}
				path.Add(Encoding.UTF8.GetString(bs, 0, bz));
				bz = 0;
			}
			if (end) {
				if (read.Lex(to - 1) == '\n') {
					Error(L.NAME, f, to, "eol unexpected");
					BackByte(to = f); // next lexi will be eol
				}
				key = read.Lex(from) != '.' ? L.NAME : L.RUN;
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

	int Read(int f, int to, int x)
	{
		var n = x + to - f;
		if (bs.Length < n)
			Array.Resize(ref bs, n + 4095 & ~4095);
		read.Lexs(f, to, bs.AsSpan(x));
		return n;
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
					Error(L.INT, from, from + bz, "hexadecimal out of range");
					return 0;
				}
		return v;
	}

	object Num(ref L key)
	{
		uint v = 0; int x = 0, dot = 0, e = 0;
		if (ni == bz) {
			key = L.INT; // as INT
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
		key = L.FLOAT; // as FLOAT
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
