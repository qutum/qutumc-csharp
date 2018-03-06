using System;
using System.Collections.Generic;

namespace qutum.parser
{
	public struct Token<K, V> : IEquatable<K> where K : IEquatable<K>
	{
		public K key;
		public V value;

		public bool Equals(K k) => key.Equals(k);
	}

	public abstract class Lexer<K, T, S> : Scan<IEnumerable<byte>, K, T, S>
		where T : struct, IEquatable<K> where S : class, IEnumerable<T>
	{
		public class Step
		{
			internal Step[] next;
			internal int mode; // 0: no key, 1: begin, 2: inside, 3: end
			internal K key;
		}

		Step start;
		protected Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan;
		List<byte> buf = new List<byte>();
		protected List<T> tokens = new List<T>();
		protected int loc;

		public Lexer(Step start, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan)
		{ this.start = start; this.scan = scan; }

		public abstract IEnumerable<object> Keys(string name);

		public void Load(IEnumerable<byte> input)
		{
			Unload();
			scan.Load(input);
		}

		public bool Next()
		{
			if (++loc < tokens.Count) return true;
			if (!scan.Next()) return false;
			byte b = scan.Token();
			// TODO
			return loc < tokens.Count;
		}

		protected abstract void Token(K key, int mode);

		protected abstract void TokenUtf(K key, int mode, int ucs);

		public bool Is(K key) => tokens[loc].Equals(key);

		public T Token() => tokens[loc];

		public abstract S Tokens(int from, int to);

		public void Tokens(int from, int to, T[] array, int ax) => tokens.CopyTo(from, array, ax, to - from);

		public void Unload() { scan.Unload(); buf.Clear(); tokens.Clear(); loc = -1; }
	}

	public abstract class LexerEnum<E, T> : Lexer<E, T, List<T>> where E : struct where T : struct, IEquatable<E>
	{
		public LexerEnum(Step start, Scan<IEnumerable<byte>, byte, byte, IEnumerable<byte>> scan) : base(start, scan) { }

		public override IEnumerable<object> Keys(string name) => new object[] { Enum.Parse<E>(name) };

		public override List<T> Tokens(int from, int to) => tokens.GetRange(from, to - from);
	}
}
