//
// Qutum 10 Compiler
// Copyright 2008-2025 Qianyan Cai
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

/*	EXC		!	NOT
	QUO		"	STR?
	NUM		#	COM (HASH)
	DOL		$
	PER		%	MOD
	AMP		&	AND
	APO		'	STR?
	LP		(	LP
	RP		)	RP
	AST		*	MUL
	PLUS	+	ADD,POSI
	COM		,	INP
	HYP		-	SUB,NEGA
	DOT		.	RUN
	SLA		/	DIV
	COL		:
	SCOL	;
	LT		<	LT
	EQ		=	quote
	GT		>	GT
	QUE		?
	AT		@
	LSB		[	LSB
	BSLA	\	byte block
	RSB		]	RSB
	HAT		^
	UND		_	NAME
	BAPO	`	PATH
	LCB		{	LCB
	VER		|	OR
	RCB		}	RCB
	TIL		~
*/

public static class LexIs
{
	public static IEnumerable<L> OfGroup(this L g, bool kinds = false)
	{
		HashSet<L> ks = kinds ? [] : null;
		foreach (var key in Enum.GetValues<L>())
			if (key.InGroupAll(g)) {
				yield return key;
				if (kinds && key.InKind() is var k and > 0)
					ks.Add((L)k);
			}
		if (kinds)
			foreach (var k in ks)
				yield return k;
	}

	public static bool IsGroup(this L aim) => (byte)aim == 0;
	public static bool IsKind(this L aim) => (ushort)aim is > 0 and < 16;
	public static bool IsSingle(this L aim) => (ushort)aim is >= 16 and <= 255;
	public static bool InGroup(this L key, L aim) => (int)(key & aim) << 8 != 0;
	public static bool InGroupAll(this L key, L aim) => (int)(key & aim) == (int)aim;
	public static byte InKind(this L key) => (byte)((ushort)key >> 8);
	public static bool InKind(this L key, L aim) => (byte)((int)key >> ((int)key >> 24)) == (byte)aim;

	public static bool Is(this L key, L aim) =>
		aim.IsGroup() ? key.InGroup(aim) : aim.IsKind() ? key.InKind(aim) : key == aim;

	// key ordinal is single or kind byte
	public static Kord Ordin(this L key) => (Kord)(byte)((int)key >> ((int)key >> 24));
}

// lexemes
public enum Lex : int
{
	// 0 for end of read
	// kinds 255<<8
	LIT = 1, // literals
	BIN1,
	BIN2,
	BIN3,  // logical binary operators
	BIN4,  // comparison binary operators
	BIN53, // arithmetic binary operators
	BIN57, // arithmetic binary operators
	BIN67, // bitwise binary operators
	BIN7,
	PRE,   // prefix operators
	POST,  // postfix operators
	POSTD, // dense postfix operators

	// singles
	QUO = 16,
	// singles of groups: binary, proem, phrase
	INP = Bin | QUO + 1 & 255, ORB, XORB,
	LP = Proem | XORB + 1 & 255, LCB, LSB = Junct | LCB + 1,
	RP = Phr | LSB + 1 & 255, RCB, RSB,
	// singles of blank group
	EOL = Blank | RSB + 1 & 255, IND, INDR, DED, DEDR, INDJ,
	SP, COM, COMB, PATH, DEC, HEX,

	// inside kinds
	// literal
	STR = Phr | Kind | LIT << 8 | 1, STRB, NAME, INT, FLOAT,
	// logical
	OR = Bin | Kind | BIN3 << 8 | 1, XOR, AND,
	// comparison
	EQ = Bin | Kind | BIN4 << 8 | 1, UEQ, LEQ, GEQ, LT, GT,
	// arithmetic
	ADD = Bin | Kind | BIN53 << 8 | 1, SUB,
	MUL = Bin | Kind | BIN57 << 8 | 1, DIV, MOD, DIVF, MODF,
	// bitwise
	SHL = Bin | Kind | BIN67 << 8 | 1, SHR, ANDB,
	// prefix: logical, arithmetic, bitwise
	NOT = Proem | Kind | PRE << 8 | 1, POSI, NEGA, NOTB,
	// postfix
	RUN = Phr | Junct | Kind | POST << 8 | 1,
	// dense postfix
	RUND = POSTD << 8 | POST << 8 ^ RUN,

	// groups 255<<16
	Proem  /**/= 0x_01_0000, // proem
	Phr    /**/= 0x_02_0000, // phrase
	Junct  /**/= 0x_04_0000, // junct
	Bin    /**/= 0x_08_0000 | Junct, // binary
	Blank  /**/= 0x_80_0000, // blank
	Kind   /**/= 0x800_0000, // kind 8<<24
}

// lexic parser
public sealed partial class Lexier : LexierBuf<L>
{
	static readonly LexGram<L> Grammar = new LexGram<L>()
		.k(L.INP).w[","].k(L.QUO).w["="]

		.k(L.OR).w["|"].k(L.XOR).w["!="].k(L.AND).w["&"].k(L.NOT).w["!"]

		.k(L.EQ).w["=="].k(L.UEQ).w["/="]
		.k(L.LEQ).w["<="].k(L.GEQ).w[">="].k(L.LT).w["<"].k(L.GT).w[">"]

		.k(L.ADD).w["+"].k(L.SUB).w["-"]
		.k(L.MUL).w["*"].k(L.DIV).w["/"].k(L.MOD).w["%"]
		.k(L.DIVF).w["//"].k(L.MODF).w["%%"]

		.k(L.ORB).w["||"].k(L.XORB).w["++"].k(L.ANDB).w["&&"].k(L.NOTB).w["--"]
		.k(L.SHL).w["<<"].k(L.SHR).w[">>"]

		.k(L.LP).w["("].k(L.LSB).w["["].k(L.LCB).w["{"]
		.k(L.RP).w[")"].k(L.RSB).w["]"].k(L.RCB).w["}"]

		.k(L.EOL).w["\n"]["\r\n"]
		.k(L.SP).w[" \t".One()] // [\s\t]  |+\s+|+\t+
				.w[[]][" ", ..].loop["\t", ..].loop

		.k(L.COM).w["##"] // ##  |[\A^\n]+
				.w[[]][Set.All.Exc("\n"), ..]
		.k(L.COMB).w["\\", .., "#"] // \\+#  +#\\+|+#|+[\A^#]+
				.w["#", "\\", ..].loop["#"].loop[Set.All.Exc("#"), ..].loop

		.k(L.STRB).w["\\", .., "\""] // \\+"  +"\\+|+"|+[\A^"]+
				.w["\"", "\\", ..].loop["\""].loop[Set.All.Exc("\""), ..].loop
		.k(L.STR).w["\""] // "  *"|\n|\r\n|+[\L^"\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["\"\n".One()]["\r\n"]
					[Set.Line.Exc("\"\\"), ..].loop
					["\\", "0tnr\".`\\".One()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.PATH).w["`"][".`"] // .?`  *`|\n|\r\n|+.|+[\L^.`\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["`\n".One()]["\r\n"]["."].loop
					[Set.Line.Exc(".`\\"), ..].loop
					["\\", "0tnr\".`\\".One()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.NAME).w[Set.Alpha.Inc("_")][Set.Alpha.Inc("_"), Set.Word, ..] // [\a_]\w*
		.k(L.RUN).w["."] // .|.[\a_]\w*
					[".", Set.Alpha.Inc("_")][".", Set.Alpha.Inc("_"), Set.Word, ..]

		.k(L.HEX).w["0x"]["0X"] // 0[xX]  _*\x+  |+_*\x+  |.\x+  |+_+\x+  |[pP][\+\-]?\d+|[xX][\+\-]?\x+
				.w[Set.Hex]["_", .., Set.Hex]
				.w[[]][Set.Hex, ..].loop["_", .., Set.Hex, ..].loop
				.w[[]][".", Set.Hex, ..]
				.w[[]]["_", .., Set.Hex, ..].loop
				.w[[]]["pP".One(), Set.Dec, ..]["pP".One(), "+-".One(), Set.Dec, ..]
					["xX".One(), Set.Hex, ..]["xX".One(), "+-".One(), Set.Hex, ..]
		.k(L.DEC) // 0|[1-9]  |+_*\d+  |.\d+  |+_+\d+  |[eE][\+\-]?\d+  |[fF]
				.w["0"][Set.Dec.Exc("0")]
				.w[[]][Set.Dec, ..].loop["_", .., Set.Dec, ..].loop
				.w[[]][".", Set.Dec, ..]
				.w[[]]["_", .., Set.Dec, ..].loop
				.w[[]]["eE".One(), Set.Dec, ..]["eE".One(), "+-".One(), Set.Dec, ..]
				.w[[]]["fF".One()]
	;

	public const int IndMin = 2, IndrMin = 6;
	public List<Lexi<L>> blanks; // [blank except single eol and indent], merged into lexis at err
	public bool dump = false;

	public Lexier() { G = new(Grammar); }

	// results keep available
	public override void Dispose()
	{
		G.buf = dump ? 65535 : 7;
		G.lines = lines; G.errs = errs; G.blanks = blanks;
		base.Dispose(); G.Dispose();
		errs.Clear();
	}
	public IDisposable Begin(Lexer<byte, byte> read)
	{
		Dispose();
		G.Begin(read); G.Next(); gloc = G.Loc();
		Add(L.IND, default, inds[indz = 0] = 0); // whole read is indent 0
		soi = size; loc = 0;
		return this;
	}

	readonly LexierGet G;
	int gloc;
	int indz; // inds size from 1
	int[] inds = new int[50]; // {0, indent column...}
	int soi; // start loc of indent

	public override bool Next()
	{
		if (loc.IncLess(size)) return true;
		var siz = size;
	Next:
		if (siz < size || G.Get(ref gloc, out var ok) is var (k, j, v) && !ok)
			return loc < size;

		switch (k) {

		case default(L):
			Indent(0, (G.left.j.via, G.left.j.via)); // eor
			break;
		case L.IND:
			break;

		case L.EOL:
			if (Left.key is >= L.EOL and <= L.INDR)
				G.Blank(k, j, null); // no eol for empty line, so no empty indent block
			else
				Add(G.lex);
			break;

		case L.ADD:
		case L.SUB:
			if ((Left.j.via < j.on || Left.key.InGroup(L.Proem | L.Blank))
				&& j.via == G.right.j.on && !G.right.key.InGroup(L.Blank))
				// left not dense and right dense, binary as prefix
				k = k switch { L.ADD => L.POSI, L.SUB => L.NEGA, _ => throw new() };
			Lexi(k, j, v);
			break;

		default:
			Lexi(k, j, v);
			break;
		}
		goto Next;
	}

	void Lexi(L k, Jov j, object v)
	{
		// indent at start of line
		if (G.left.key == L.EOL)
			Indent(0, (j.on, j.on));
		else if (G.left.key == L.IND)
			Indent((int)G.left.value, G.left.j);

		// left dense
		if (Left.j.via == j.on)
			if (k.InKind(L.POST) && Left.key.InGroup(L.Phr)) // postfix densely follows phrase, higher precedence
				k = (L)((int)k ^ (int)L.POST << 8 | (int)L.POSTD << 8);
			else if (k.InKind(L.LIT) && Left.key.InKind(L.LIT))
				G.Error(k, j, "literal can not densely follow literal");
			else if (k.InKind(L.PRE) && !Left.key.InGroup(L.Proem | L.Blank))
				G.Error(k, j, "prefix operator can densely follow proem or blank only");

		// junct at start of line
		if (G.left.key is L.EOL or L.IND && k.InGroup(L.Junct)) {
			Indent((G.left.key == L.IND ? (int)G.left.value : 0) + 3, (j.on, j.on), true); // between 2 and 4 column
			do
				Add(k, j, v);
			while (G.right.key.InKind(L.POST) // multi post as junct
				&& ((k, j, v) = G.Get(ref gloc, out var _)) is var _);
			if (!k.InGroup(L.Proem))
				Add(L.EOL, (j.via, j.via), null);
			soi = size; // start loc of junct indent
		}
		else
			Add(k, j, v);
	}
	void Add(L k, Jov j, object v) => Add(new() { key = k, j = j, value = v });

	// indent column from 0
	void Indent(int ind, Jov j, bool junct = false)
	{
		var i = Array.BinarySearch(inds, 1, indz, ind + IndMin); // indent with offset
		i ^= i >> 31; // >= 1
		var c = ind - inds[i - 1];
		// for i <= indz, drop these indents
		for (var x = indz; i <= x; indz = --x)
			if (x > i || c < IndrMin)
				Add(inds[x] - inds[x - 1] < IndrMin ? L.DED : L.DEDR,
					(j.on, j.on), inds[x]);
			else { // indent-right remains
				G.Error(L.INDR, j, "indent-right expected same as upper lines");
				inds[i] = ind;
				return;
			}
		// add indent
		if (c >= IndMin) {
			if (soi == size) // indent at start of indent, insert eol
				Add(L.EOL, (j.on, j.on), null);
			Add(c < IndrMin ? junct ? L.INDJ : L.IND : L.INDR, j, ind);
			indz = i;
			if (indz >= inds.Length) Array.Resize(ref inds, inds.Length << 1);
			inds[i] = ind;
			soi = size;
		}
	}

	Lexi<L> Left => lexs[size - 1];

	public static Kord Ordin(Lexier ler) => ler.lexs[ler.loc].key.Ordin();
	public override bool Is(L key, L aim) => key.Is(aim);
}

// lexic parser base
internal sealed class LexierGet : Lexier<L>, Lexer<L, Lexi<L>>
{
	public int buf;
	public List<Lexi<L>> blanks; // [blank except single eol and indent], merged into lexis at err

	public LexierGet(LexGram<L> gram) : base(gram) { errs = null; }

	public override void Dispose()
	{
		base.Dispose();
		lexs = new Lexi<L>[buf + 1];
		lexs[^1].key = L.EOL; // eol at start of read
		crlf = false; indb = -1; path.Clear();
	}

	public Lexi<L> left, lex, right;
	public (L k, Jov j, object v) Get(ref int l, out bool ok)
	{
		ok = Next() || l < loc;
		if (!ok)
			return default;
		left = lexs[l - 1 & buf]; lex = lexs[l++ & buf]; right = lexs[l & buf];
		return (lex.key, lex.j, lex.value);
	}

	protected override void Add(Lexi<L> lexi) => lexs[size++ & buf] = lexi;
	public new void Error(L k, Jov j, object value) => base.Error(k, j, value);
	public void Blank(L k, Jov j, object value) => blanks?.Add(
		new() { key = k, j = j, value = value, err = 1 });

	protected override void WadErr(L k, int wad, bool end, int b, Jov j)
	{
		if (k == L.PATH)
			k = L.NAME; // TODO
		base.WadErr(k, wad, end, b, j);
	}
	protected override void Eor(int via)
	{
		if (size == 0 || lexs[size - 1 & buf].key != L.EOL)
			Lexi(L.EOL, (via, via), null); // eol at end of read
		Lexi(default, (via, via), null);
	}

	int bz; // buffer size
	byte[] bs = new byte[4096]; // long read buffer
	bool crlf; // \r\n found
	bool leadSp; // leading space
	int indb; // indent byte, unknown: -1
	int ni, nf, ne; // end of each number wad
	readonly List<string> path = [];

	protected override void Wad(L k, int wad, ref bool end, Jov j)
	{
		object d = null;
		if (lon < 0) {
			lon = j.on; bz = 0; path.Clear();
		}
		Jov lj = (lon, j.via);
		switch (k) {

		case L.EOL:
			if (!crlf && j.size == 2) { // \r\n found
				Error(k, j, @"use LF \n eol instead of CRLF \r\n");
				crlf = true;
			}
			Lexi(k, lj, null);
			goto End;

		case L.SP:
			if (wad == 1)
				leadSp = j.on < 1 || read.Lex(j.on - 1) == '\n'; // maybe leading space
			if (leadSp)
				if (indb < 0)
					indb = read.Lex(j.on); // first leading space, save the indent byte
				else if (j.any && read.Lex(j.on) != indb) { // mix \s and \t
					leadSp = false; // not leading and indent unchanged
					Error(k, j, "do not mix tabs and spaces for indent"); // TODO omit this before comment ?
				}
			if (!end)
				return;
			if (leadSp)
				Lexi(L.IND, lj, lj.size << (indb == '\t' ? 2 : 0)); // 4 column each \t
			else
				Blank(k, lj, null);
			goto End;

		case L.COM:
			if (end)
				Blank(k, lj, null);
			goto End;

		case L.COMB:
			if (wad == 1) {
				bz = j.via; // begin wad loc
				return;
			}
			if (j.size != bz - lon || read.Lex(j.on) != '#') // check end wad size
				return;
			end = true; bz = 0;
			Blank(k, lj, null);
			goto End;

		case L.STRB:
			if (wad == 1) {
				bz = j.via; // begin wad loc
				return;
			}
			if (j.size != bz - lon || read.Lex(j.on) != '"') // check end wad size
				return;
			end = true; bz = Read((bz, j.on), 0);
			break;

		case L.STR:
			if (wad == 1)
				return;
			if (end) {
				if (read.Lex(j.via - 1) == '\n') {
					Error(k, j, "eol unexpected");
					BackByte(lj.via = j.via = j.on); // next lexi will be eol
				}
				break;
			}
			Read(j, bz); Unesc(j);
			return; // inside string

		case L.DEC:
			if (wad == 2) ni = lj.size; // end of integer wad
			else if (wad == 4) nf = lj.size; // end of fraction wad
			else if (wad == 5) ne = lj.size; // end of exponent wad
			if (!end)
				return;
			bz = Read(lj, 0);
			d = Dec(ref k);
			break;

		case L.HEX:
			if (wad == 3) ni = lj.size; // end of integer wad
			else if (wad == 5) nf = lj.size; // end of fraction wad
			else if (wad == 6) ne = lj.size; // end of exponent wad
			if (!end)
				return;
			bz = Read(lj, 0);
			d = Hex(ref k);
			break;

		case L.NAME:
		case L.RUN:
			j.on = k == L.NAME ? lon : lon + 1;
			if (j.size > 40) {
				Error(k, j, "too long");
				lj.via = j.via = j.on + 40;
			}
			bz = Read(j, 0);
			break;

		case L.PATH:
			if (wad == 1)
				return;
			var split = end || read.Lex(j.on) == '.';
			if (split) {
				if (bz > 40) {
					Error(L.NAME, (j.via - 1, j.via - 1), "too long");
					bz = 40;
				}
				path.Add(Encoding.UTF8.GetString(bs, 0, bz));
				bz = 0;
			}
			if (end) {
				if (read.Lex(j.via - 1) == '\n') {
					Error(L.NAME, j, "eol unexpected");
					BackByte(lj.via = j.via = j.on); // next lexi will be eol
				}
				k = read.Lex(lon) != '.' ? L.NAME : L.RUN;
				d = path.ToArray();
				break;
			}
			if (!split) {
				Read(j, bz); Unesc(j);
			}
			return; // inside path
		}
		if (!end)
			return;
		Lexi(k, lj, d ?? (bz > 0 ? Encoding.UTF8.GetString(bs, 0, bz) : null));
	End: // lexi already made
		lon = -1;
	}

	int Read(Jov j, int x)
	{
		var n = x + j.size;
		if (bs.Length < n)
			Array.Resize(ref bs, n + 4095 & ~4095);
		read.Lexs(j, bs.AsSpan(x));
		return n;
	}

	void Unesc(Jov j)
	{
		if (bs[bz] != '\\')
			bz += j.size;
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

	object Dec(ref L key)
	{
		uint v = 0; int x = 0, dot = 0, e = 0;
		if (ni == bz) {
			key = L.INT; // as INT
			for (; x < ni; x++)
				if (bs[x] != '_')
					if (v < 21_4748_364 || v == 21_4748_364 && bs[x] <= '8') v = v * 10 + bs[x] - '0';
					else {
						Error(key, (lon, lon + ni), "integer out of range");
						return 0;
					}
			return v;
		}
		key = L.FLOAT; // as FLOAT
		if (bz > 65535) {
			Error(key, (lon, lon + bz), "float out of length");
			return 0f;
		}
		for (; x < ni; x++)
			if (bs[x] != '_')
				if (v <= 9_9999_999) v = v * 10 + bs[x] - '0';
				else dot++;
		for (x = ni + 1; x < nf; x++)
			if (bs[x] != '_')
				if (v <= 9_9999_999) (v, dot) = (v * 10 + bs[x] - '0', dot - 1);
				else break;
		if (v == 0)
			return 0f;
		if (nf < ne) {
			bool neg = bs[nf + 1] == '-';
			for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; ++x < ne;)
				if (e <= 9_9999_999) e = e * 10 + bs[x] - '0';
				else {
					Error(key, (lon, lon + ne), "exponent out of range");
					return 0f;
				}
			if (neg)
				e = -e;
		}
		e += dot;
		if (e <= 0)
			return e < -54 ? 0f : e < -38 ? v / Exps[-e - 38] / Exps[38] : v / Exps[-e];
		float d = v * Exps[e < 39 ? e : 39];
		if (float.IsInfinity(d)) {
			Error(key, (lon, lon + bz), "float out of range");
			return 0f;
		}
		return d;
	}

	static readonly float[] Exps = [
		1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
		1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
		1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
		1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
	];

	byte Hex(int x) => (byte)((bs[x] & 15) + (bs[x] << 25 >> 31 & 9));

	object Hex(ref L key)
	{
		uint v = 0; int x = 2, dot = 0, e = 0;
		if (ni == bz) {
			key = L.INT; // as INT
			for (; x < ni; x++)
				if (bs[x] != '_')
					if (v <= 0xffff_fff) v = v << 4 | Hex(x);
					else {
						Error(key, (lon, lon + ni), "hexadecimal out of range");
						return 0;
					}
			return v;
		}
		key = L.FLOAT; // as FLOAT
		if (bz > 65535) {
			Error(key, (lon, lon + bz), "float out of length");
			return 0f;
		}
		for (; x < ni; x++)
			if (bs[x] != '_')
				if (v <= 0xffff_fff) v = v << 4 | Hex(x);
				else dot++;
		for (x = ni + 1; x < nf; x++)
			if (bs[x] != '_')
				if (v <= 0xffff_fff) (v, dot) = (v << 4 | Hex(x), dot - 1);
				else break;
		if (v == 0)
			return 0f;
		if (nf < ne) {
			bool hex = (char)bs[nf] is 'x' or 'X';
			bool neg = bs[nf + 1] == '-';
			for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; ++x < ne;)
				if (hex ? e <= 0x6fff_fff : e <= 9_9999_999)
					e = hex ? e << 4 | Hex(x) : e * 10 + bs[x] - '0';
				else {
					Error(key, (lon, lon + ni), "exponent out of range");
					return 0f;
				}
			if (neg)
				e = -e;
		}
		e += dot << 2;
		if (e <= 0)
			return e < -181 ? 0f : e < -126 ? v / Pows[-e - 126] / Pows[126] : v / Pows[-e];
		float d = v * Pows[e < 128 ? e : 128];
		if (float.IsInfinity(d)) {
			Error(key, (lon, lon + bz), "float out of range");
			return 0f;
		}
		return d;
	}

	static readonly float[] Pows = Enumerable.Range(0, 129)
		.Select(x => BitConverter.Int32BitsToSingle(x + 127 << 23)).ToArray();
}
