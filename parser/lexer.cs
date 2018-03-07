using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser
{
	public abstract class Lexer<K, T, S> : Scan<IEnumerable<byte>, K, T, S> where T : struct where S : IEnumerable<T>
	{
		public class Tran
		{
			internal Tran[] next; // utf-8: <=bf: [128], <=df: [129], <=ef: [130], <=f7: [131], ff: [132]
			internal K key;
			internal int step;
			internal int mode; // err: 0 (next != null), back: 1 (no repeatition), ok: 2
			internal Tran go; // go.next != null
		}

		Tran start;
		int bf, bt, bn;
		internal Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		internal byte[] buf = new byte[16];
		internal List<T> tokens = new List<T>();
		internal int loc = -2;

		public Lexer(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{ this.scan = scan; Boot(grammar); }

		public abstract IEnumerable<object> Keys(string name);

		public void Load(IEnumerable<byte> input)
		{
			if (loc > -2) Unload();
			scan.Load(input);
			bf = bt = bn = scan.Loc() + 1; loc = -1;
		}

		public void Unload() { scan.Unload(); tokens.Clear(); loc = -2; }

		public bool Next()
		{
			if (++loc < tokens.Count) return true;
			var t = start;
			Go: bf = bt;
			Next: if (bt >= bn)
				if (scan.Next()) buf[bn++ & 15] = scan.Token();
				else if (t == start) return false;
				else goto Do;
			var b = buf[bt & 15];
			if (t.next[b <= 0x7f ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128] is Tran n)
			{
				t = n; ++bt;
				if (t.next != null) goto Next;
			}
			Do: switch (t.mode)
			{
				case 1: t = t.go; --bt; goto Do;
				case 2: Token(t.key, t.step, bf, bt); break;
				default: Error(t.key, t.step, buf[bt & 15], bf, ++bt); break;
			}
			if (t.go == start && loc < tokens.Count) return true;
			t = t.go; goto Go;
		}

		protected abstract bool Token(K key, int step, int from, int to);

		protected abstract void Error(K key, int step, byte b, int from, int to);

		public abstract bool Is(K key, object keyo);

		public int Loc() => loc;

		public T Token() => tokens[loc];

		public abstract S Tokens(int from, int to);

		public void Tokens(int from, int to, T[] array, int ax) => tokens.CopyTo(from, array, ax, to - from);

		static Earley<string, char, char, string> boot = new Earley<string, char, char, string>(@"
			gram  = eol*lex lexs*eol*
			lexs  = eol+lex
			lex   = name S*\=S*step steps* =+
			steps = S+step
			step  = byte+ alt* =+
			alt   = \|byte+
			byte  = B rep? | [part+]rep? | \\E rep?
			name  = W+ =+
			rep   = R =+
			part  = ^?P | ^?P-P
			eol   = S*\r?\nS*", new BootScan()) { greedy = false, treeKeep = false, treeDump = false };

		void Boot(string grammar)
		{
			boot.treeDump = true;
			var t = boot.Parse(grammar).Dump();
		}
	}

	public struct Token<K> where K : struct
	{
		public K key;
		public int from, to; // input loc
		public bool err;
		public object value;
	}

	// utf key: Utf, error key: Err
	public class LexerEnum<K> : Lexer<K, Token<K>, List<Token<K>>> where K : struct
	{
		K kutf;

		public LexerEnum(string grammar, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan = null)
			: base(grammar, scan ?? new ScanByte())
		{
			kutf = (K)Keys("Utf").First();
		}

		public override IEnumerable<object> Keys(string name) => new object[] { Enum.Parse<K>(name) };

		protected override void Error(K key, int step, byte b, int from, int to)
			=> tokens.Add(new Token<K> { key = key, from = from, to = to, err = true, value = b });

		protected override bool Token(K key, int step, int from, int to)
		{
			//TODO
			return false;
		}

		public override bool Is(K key, object keyo) => tokens[loc].key.Equals(keyo);

		public override List<Token<K>> Tokens(int from, int to) => tokens.GetRange(from, to - from);
	}
}
