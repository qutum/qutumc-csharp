using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace qutum.parser
{
	public struct Token<K, V> : IEquatable<K> where K : IEquatable<K>
	{
		public K key;
		public int from, to; // input loc
		public V value;

		public bool Equals(K k) => key.Equals(k);
	}

	public abstract class Lexer<K, T, S> : Scan<IEnumerable<byte>, K, T, S>
		where T : struct, IEquatable<K> where S : class, IEnumerable<T>
	{
		public class Tran
		{
			internal Tran prev;
			internal Tran[] next; // utf-8: <=bf: [128], <=df: [129], <=ef: [130], <=f7: [131], ff: [132]
			internal int mode; // 0: no key, 1: begin, 2: inside, 3: end
			internal K key;
		}

		Tran start;
		protected Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		protected byte[] buf = new byte[8];
		protected List<T> tokens = new List<T>();
		protected int bf, bt, bn, loc = -2;

		public Lexer(Tran start, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{ this.start = start; this.scan = scan; }

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
			Start: var t = start; bf = bt;
			Next: if (bt == bn)
				if (scan.Next()) { buf[bn] = scan.Token(); ++bn; }
				else if (t == start) return false;
				else goto Do;
			var b = buf[bt & 7];
			if (t.next[b <= 0x7f ? b : b > 0xf7 ? 132 : b > 0xef ? 131 : b > 0xdf ? 130 : b > 0xbf ? 129 : 128] is Tran n)
			{
				t = n; ++bt;
				if (t.next != null) goto Next;
			}
			Do: if (t == start)
			{
				Debug.Assert(bf == bt);
				// Err buf[bf&7]
				goto Next;
			}
			if (t.mode == 0) { t = t.prev; --bt; goto Do; }
			// Do buf[bf&7,bt&7)
			if (loc == tokens.Count) goto Start;
			return true;
		}

		protected abstract void Err();

		protected abstract void Token(K key, int mode);

		protected abstract void TokenUtf(K key, int mode, int ucs);

		public bool Is(K key) => tokens[loc].Equals(key);

		public int Loc() => loc;

		public T Token() => tokens[loc];

		public abstract S Tokens(int from, int to);

		public void Tokens(int from, int to, T[] array, int ax) => tokens.CopyTo(from, array, ax, to - from);
	}

	public abstract class LexerEnum<E, T> : Lexer<E, T, List<T>> where E : struct where T : struct, IEquatable<E>
	{
		public LexerEnum(Tran start, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan) : base(start, scan) { }

		public override IEnumerable<object> Keys(string name) => new object[] { Enum.Parse<E>(name) };

		public override List<T> Tokens(int from, int to) => tokens.GetRange(from, to - from);
	}
}
