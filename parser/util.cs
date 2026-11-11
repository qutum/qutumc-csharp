//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
global using Jot = qutum.parser.Jot<int>;
global using Jov = qutum.parser.Jov<int>;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace qutum.parser;

#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
// typed index, meaningless _ is for distinguish only
public struct X<_> : IUnaryPlusOperators<X<_>, int>, IComparisonOperators<X<_>, X<_>, bool>,
	IAdditionOperators<X<_>, int, X<_>>, IBitwiseOperators<X<_>, X<_>, X<_>>, IComparable<X<_>>
{
	int x;
	public readonly bool zero => x == 0;
	public readonly bool pos => x > 0;
	public readonly bool neg => x < 0;
	public readonly bool zpos => x >= 0;
	public readonly bool zneg => x <= 0;
	public readonly bool yes => x != -1;
	public readonly bool no => x == -1;
	public static readonly X<_> No = (X<_>)(-1);

	public static explicit operator X<_>(int x) => new() { x = x };
	public override readonly string ToString() => x.ToString();

	public static int operator +(X<_> a) => a.x;
	public static bool operator ==(X<_> a, X<_> b) => a.x == b.x;
	public static bool operator !=(X<_> a, X<_> b) => a.x != b.x;
	public static bool operator <(X<_> a, X<_> b) => a.x < b.x;
	public static bool operator <=(X<_> a, X<_> b) => a.x <= b.x;
	public static bool operator >(X<_> a, X<_> b) => a.x > b.x;
	public static bool operator >=(X<_> a, X<_> b) => a.x >= b.x;
	public static X<_> operator +(X<_> a, X<_> b) => (X<_>)(a.x + b.x);
	public static X<_> operator -(X<_> a, X<_> b) => (X<_>)(a.x - b.x);
	public static X<_> operator +(X<_> a, int b) => (X<_>)(a.x + b);
	public static X<_> operator -(X<_> a, int b) => (X<_>)(a.x - b);
	public static X<_> operator |(X<_> a, X<_> b) => (X<_>)(a.x | b.x);
	public static X<_> operator &(X<_> a, X<_> b) => (X<_>)(a.x & b.x);
	public static X<_> operator ^(X<_> a, X<_> b) => (X<_>)(a.x ^ b.x);
	public static X<_> operator ~(X<_> a) => (X<_>)~a.x;
	public readonly int CompareTo(X<_> that) => x - that.x;
}
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)

// jot of data to inclusive end, [on,to], default size should be 1
public struct Jot<X> : IEnumerable<X> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	public X on; // index on the first, [on]==first
	public X to; // inclusive index to the last, last==[to]

	public readonly X size => to + -+on + 1;
	public readonly bool any => +on <= +to;
	public readonly Range range => +on..+(to + 1);
	public readonly void Deconstruct(out X on, out X to) => (on, to) = (this.on, this.to);

	public static implicit operator Jot<X>((X on, X to) j) => new() { on = j.on, to = j.to };
	public static implicit operator (X on, X to)(Jot<X> j) => (j.on, j.to);
	public static implicit operator Jot<X>(Jov<X> j) => (j.on, j.via + -1);
	public static explicit operator Jot<int>(Jot<X> j) => (+j.on, +j.to);

	public readonly IEnumerator<X> GetEnumerator() { for (X x = on; +x <= +to; x += 1) yield return x; }
	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
// jot of data via exclusive end, [on,via), default size should be 0
public struct Jov<X> : IEnumerable<X> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	public X on; // index on the first, [on]==first
	public X via; // exclusive index via the last, last==[via-1]

	public readonly X size => via + -+on;
	public readonly bool any => +on < +via;
	public readonly Range range => +on..+via;
	public readonly void Deconstruct(out X on, out X via) => (on, via) = (this.on, this.via);

	public static implicit operator Jov<X>((X on, X via) j) => new() { on = j.on, via = j.via };
	public static implicit operator (X on, X via)(Jov<X> j) => (j.on, j.via);
	public static implicit operator Jov<X>(Jot<X> j) => (j.on, j.to + 1);
	public static explicit operator Jov<int>(Jov<X> j) => (+j.on, +j.via);

	public readonly IEnumerator<X> GetEnumerator() { for (X x = on; +x < +via; x += 1) yield return x; }
	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public struct Arr<X, T> : IEnumerable<T> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	public T[] s;
	public Jov<X> j;
	public readonly X size => j.size;
	public readonly ref T this[X x] { get => ref Span[+x]; }
	public readonly ref T last => ref Span[^1];
	public readonly ref T Last(X x) => ref Span[^+x];
	public readonly T[] S => s;

	public Arr() => s = [];
	public Arr(X size) : this(+size > 0 ? new T[+size] : []) { }
	public Arr(T[] s) { this.s = s; j = (default, default(X) + s.Length); }
	public Arr(X size, Jov<X> j) : this(+size > 0 ? new T[+size] : [], j) { }
	public Arr(T[] s, Jov<X> j) { this.s = s; this.j = j; _ = Span; }

	public Arr<X, T> Add(T d)
	{
		if (+j.via >= s.Length)
			Array.Resize(ref s, +j.via + (s.Length < 16384 ? int.Max(s.Length, 1024) : s.Length >> 1));
		s[+j.via] = d; j.via += 1;
		return this;
	}
	public Arr<X, T> Remove(X x) => Remove((x, x + 1));
	public Arr<X, T> Remove(Jov<X> j_)
	{
		if (Seg(j_) is { j: var seg } && seg.any) {
			Array.Copy(s, +seg.via, s, +seg.on, +j.via - +seg.via);
			j = (j.on, j.via + -+seg.size);
		}
		return this;
	}
	public Arr<X, T> Resize(X size, bool keepJ = false) => Resize(size, keepJ ? j : (default, size));
	public Arr<X, T> Resize(X size, Jov<X> j)
	{
		Array.Resize(ref s, +size); this.j = j; _ = Span; return this;
	}
	public Arr<X, T> Clear()
	{
		if (s.Length <= 1024) Array.Clear(s); else s = [];
		j = default; return this;
	}

	public readonly Span<T> Span => ((Span<T>)s)[j.range];
	public readonly Arr<X, T> Seg(Jov<X> j) => new(s, (this.j.on + +j.on, this.j.on + +j.via));
	public readonly Arr<X, T> Seg(int on, X via) => Seg((default(X) + on, via));

	public readonly IEnumerator<T> GetEnumerator()
	{
		for (int x = +j.on, v = +j.via; x < v; x++) yield return s[x];
	}
	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public readonly IEnumerable<(T d, X x)> Each(X offset = default)
	{
		for (int x = +j.on, v = +j.via; x < v; x++) yield return (s[x], offset + x);
	}

	public readonly X BinFind(T v, bool xNo = true) =>
		default(X) + (Array.BinarySearch(s, +j.on, +j.size, v) is var x && x < 0 && xNo ? -1 : x);
}
public class List<X, T> : IEnumerable<T> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	Arr<X, T> s;
	public X size => s.size;
	public ref T this[X x] { get => ref s[x]; }
	public ref T last => ref s.last;
	public ref T Last(X x) => ref s.Last(x);

	public List(X size = default) => s = new(size);
	public List<X, T> Add(T d) { s.Add(d); return this; }
	public List<X, T> Remove(X x) { s.Remove(x); return this; }
	public List<X, T> Remove(Jov<X> j) { s.Remove(j); return this; }
	public virtual List<X, T> Clear() { s.Clear(); return this; }

	public IEnumerator<T> GetEnumerator() => s.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerable<(T d, X x)> Each(X offset = default) => s.Each(offset);
}

public static partial class Extension
{
	public static Jot<X<_>> X<_>(this Jot j) => ((X<_>)j.on, (X<_>)j.to);
	public static Jov<X<_>> X<_>(this Jov j) => ((X<_>)j.on, (X<_>)j.via);

	public static X<_> Min<_>(this X<_> a, X<_> b) => a < b ? a : b;
	public static X<_> Max<_>(this X<_> a, X<_> b) => a < b ? b : a;
	public static bool IncLess(this ref int d, int z) => ++d < z || (d = z) < z;
	public static bool IncLess<_>(this ref X<_> d, int z) => +(d += 1) < z || +(d = (X<_>)z) < z;

	public static bool Adds<T>(this ISet<T> s, IEnumerable<T> add)
	{
		if (add == null) return false;
		var z = s.Count; s.UnionWith(add); return s.Count > z;
	}

	extension<T>(IEnumerable<T> s)
	{
		public IEnumerable<(T d, int x)> Each(int offset = 0)
		{
			foreach (var d in s) yield return (d, offset++);
		}
		public Arr<X, T> ToArr<X>(X offset = default)
			where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
		{
			Arr<X, T> a = new(s is ICollection<T> c ? offset + c.Count : offset);
			foreach (var d in s) a.Add(d);
			return a;
		}

		public Dictionary<T, V> ToDict<V>(Func<T, V, V> value)
		{
			var kv = new Dictionary<T, V>();
			foreach (var d in s)
				kv[d] = kv.TryGetValue(d, out var v) ? value(d, v) : value(d, default);
			return kv;
		}
		public Dictionary<K, V> ToDict<K, V>(Func<T, K> key, Func<K, V, V> value)
		{
			var kv = new Dictionary<K, V>();
			foreach (var d in s) {
				var k = key(d);
				kv[k] = kv.TryGetValue(k, out var v) ? value(k, v) : value(k, default);
			}
			return kv;
		}
		public Dictionary<T, int> ToCount() => ToDict<T, int>(s, (d, z) => z + 1);
	}

	extension<T>(IEnumerable<T> s) where T : struct
	{
		public IEnumerable<T?> May()
		{
			bool no = true;
			foreach (var d in s) { no = false; yield return d; }
			if (no) yield return null;
		}
		public T? FirstOrNull(Func<T, bool> If)
		{
			foreach (var d in s) if (If(d)) return d;
			return null;
		}
	}
}

public partial class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
{
	public T up, prev, next, head, tail;

	public T First()
	{
		var t = (T)this;
		while (t.prev != null)
			t = t.prev;
		return t;
	}

	public T Last()
	{
		var t = (T)this;
		while (t.next != null)
			t = t.next;
		return t;
	}

	// after this.tail add sub and all sub.next
	public T Add(T sub)
	{
		if (sub == null)
			return (T)this;
		Debug.Assert(sub.up == null && sub.prev == null);
		var end = sub;
		for (end.up = (T)this; end.next != null; end.up = (T)this)
			end = end.next;
		if (head == null)
			head = sub;
		else
			(sub.prev = tail).next = sub;
		tail = end;
		return (T)this;
	}

	// before this.head add sub and all sub.next 
	public T AddHead(T sub)
	{
		if (sub == null)
			return (T)this;
		Debug.Assert(sub.up == null && sub.prev == null);
		var end = sub;
		for (end.up = (T)this; end.next != null; end.up = (T)this)
			end = end.next;
		if (head == null)
			tail = end;
		else
			(end.next = head).prev = end;
		head = sub;
		return (T)this;
	}

	// after tail add subs of t
	public T AddSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		var x = t.head;
		t.head = t.tail = null;
		x.up = null;
		return Add(x);
	}

	// after last this.next append next and all next.next
	public T Append(T next)
	{
		if (next == null)
			return (T)this;
		Debug.Assert(up == null && next.up == null && next.prev == null);
		(next.prev = Last()).next = next;
		return (T)this;
	}

	// after last this.next append subs of t
	public T AppendSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		Debug.Assert(up == null);
		var x = t.head;
		t.head = t.tail = null;
		(x.prev = Last()).next = x;
		for (x.up = null; x.next != null; x.up = null)
			x = x.next;
		return (T)this;
	}

	// remove self from up
	public T Remove(bool clear = true)
	{
		prev?.next = next;
		next?.prev = prev;
		if (up?.head == this)
			up.head = next;
		if (up?.tail == this)
			up.tail = prev;
		if (clear)
			up = prev = next = null;
		return (T)this;
	}

	// enumerate subs
	public IEnumerator<T> GetEnumerator()
	{
		for (var x = head; x != null; x = x.next)
			yield return x;
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public IEnumerable<T> Backward()
	{
		for (var x = tail; x != null; x = x.prev)
			yield return x;
	}
}

public struct BitSet : IEnumerable<int>
{
	public ulong[] bits;
	public readonly bool Empty => size == 0;
	readonly int size => bits?.Length ?? 0;

	public BitSet Use(int max)
	{
		bits ??= new ulong[(max + 63) >> 6]; return this;
	}
	public readonly BitSet Or(int x)
	{
		bits[x >> 6] |= 1UL << (x & 63); return this;
	}
	public readonly bool Or(BitSet s)
	{
		if (bits == s.bits) return false;
		var more = false;
		for (int z = s.size, x = 0; x < z; x++)
			more |= bits[x] != (bits[x] |= s.bits[x]);
		return more;
	}
	public readonly BitSet Or(IEnumerable<int> s)
	{
		foreach (var x in s ?? [])
			Or(x);
		return this;
	}
	public readonly BitSet NewOr(BitSet s, bool may = false)
	{
		var p = bits; var q = s.bits; int y = size, z = s.size;
		if (may ? p == q || z == 0 : y == 0 && z == 0)
			return this;
		if (y > z)
			(p, q, y, z) = (q, p, z, y);
		BitSet copy = new() { bits = new ulong[z] };
		for (var x = 0; x < y; x++)
			copy.bits[x] = p[x] | q[x];
		for (var x = y; x < z; x++)
			copy.bits[x] = q[x];
		return copy;
	}
	public readonly bool Same(BitSet s)
	{
		if (bits == s.bits) return true;
		if (size != s.size) return false;
		for (var x = 0; x < bits.Length; x++)
			if (bits[x] != s.bits[x])
				return false;
		return true;
	}
	public static BitSet One(int max, int x) => new BitSet().Use(max).Or(x);

	public readonly int Min()
	{
		for (int x = 0, y = 0; x < bits.Length; x++, y = 0)
			for (ulong b = bits[x]; b != 0; b >>= 1, y++)
				if ((b & 1ul) != 0) return x << 6 | y;
		return int.MaxValue;
	}
	public readonly int Max()
	{
		for (int x = size - 1, y = 63; x >= 0; x--, y = 63)
			for (ulong b = bits[x]; b != 0; b <<= 1, y--)
				if ((long)b < 0) return x << 6 | y;
		return int.MinValue;
	}
	public readonly IEnumerator<int> GetEnumerator()
	{
		for (int x = 0, y = 0; x < size; x++, y = 0)
			for (ulong b = bits[x]; b != 0; b >>= 1, y++)
				if ((b & 1ul) != 0) yield return x << 6 | y;
	}
	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// meaningless _ is for distinguish only
public struct BitSet<_> : IEnumerable<X<_>>
{
	public BitSet set;
	public readonly bool Empty => set.Empty;

	public BitSet<_> Use(X<_> max) { set.Use(+max); return this; }
	public readonly BitSet<_> Or(X<_> x) { set.Or(+x); return this; }
	public readonly bool Or(BitSet<_> s) => set.Or(s.set);
	public readonly BitSet<_> Or(IEnumerable<X<_>> s)
	{
		foreach (var x in s ?? []) set.Or(+x); return this;
	}
	public readonly BitSet<_> NewOr(BitSet<_> s, bool may = false)
	{
		var set = this.set.NewOr(s.set, may);
		return set.bits == this.set.bits ? this : new() { set = set };
	}
	public readonly bool Same(BitSet<_> s) => set.Same(s.set);
	public static BitSet<_> One(X<_> max, X<_> x) => new BitSet<_>().Use(max).Or(x);

	public readonly X<_> Min() => (X<_>)set.Min();
	public readonly X<_> Max() => (X<_>)set.Max();
	public readonly IEnumerator<X<_>> GetEnumerator()
	{
		foreach (int x in set) yield return (X<_>)x;
	}
	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0250:Make struct 'readonly'")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0251:Make member 'readonly'")]
public struct StrMake
{
	internal readonly StringBuilder s;

	public StrMake() => s = new();
	public StrMake(out StrMake s) : this() => s = this;
	public static implicit operator StrMake(string s) => new StrMake() + s;
	public static implicit operator string(StrMake s) => s.ToString();

	public static StrMake operator +(StrMake s, object d)
	{
		if (d is not StrMake m) s.s.Append(d);
		else if (m.s != s.s) s.s.Append(m.s);
		return s;
	}
	public static StrMake operator -(StrMake s, object d) => s.s.Length > 0 ? s + d : s;
	public StrMake F(string format, params object[] args)
	{
		s.AppendFormat(format, args); return this;
	}

	public int Size => s.Length;
	public override string ToString() => s.ToString();
}

public class EnvWriter : IndWriter, IDisposable
{
	protected EnvWriter() : base(null) { }

	static readonly EnvWriter env = new();
	bool pressKey;

	public static EnvWriter Use(bool pressKey)
	{
		env.pressKey = pressKey;
		return Use();
	}
	public new static EnvWriter Use()
	{
		if (env.output == null) {
			Console.OutputEncoding = new UTF8Encoding(false, false);
			env.output = Console.Out;
			Console.SetOut(env);
		}
		return (EnvWriter)((IndWriter)env).Use();
	}

	protected override void Dispose(bool _)
	{
		if (output != null && indents.Count == 0) {
			Console.SetOut(output);
			output = null;
			try {
				if (pressKey && !Console.IsInputRedirected
						&& GetConsoleProcessList(procs, 1) == 1) {
					Write("Press Any Key ... "); Flush();
					Console.ReadKey(true);
				}
			}
			catch (DllNotFoundException) { }
		}
		base.Dispose(true);
	}

	public new static EnvWriter Indent(string ind = "\t", string ind2 = null)
		=> (EnvWriter)((IndWriter)env).Indent(ind, ind2);

	protected override void Print(ReadOnlyMemory<char>? s)
	{
		base.Print(s);
		if (Debugger.IsAttached) Debug.Write(s);
	}
	protected override void Print() { base.Print(); if (Debugger.IsAttached) Debug.Flush(); }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054")]
	[DllImport("kernel32.dll", SetLastError = false)]
	static extern uint GetConsoleProcessList(uint[] procs, uint n);
	static readonly uint[] procs = new uint[1];
}

public class IndWriter(TextWriter output) : StringWriter, IDisposable
{
	protected TextWriter output = output;
	protected readonly List<(string ind, string ind2)> indents = [];
	protected bool lineStart = true;

	public IndWriter Use() => Indent("");
	public IndWriter Indent(string ind = "\t", string ind2 = null)
	{
		indents.Add((ind ?? throw new ArgumentNullException(nameof(ind)), ind2));
		return this;
	}

	protected override void Dispose(bool _)
	{
		if (indents.Count > 0)
			indents.RemoveAt(indents.Count - 1);
	}

	public override void Flush()
	{
		base.Flush();
		foreach (var cs in GetStringBuilder().GetChunks()) {
			int x = 0, z = 0;
			while (z >= 0 && x < cs.Length) {
				z = cs.Span[x..].IndexOfAny('\n', '\r');
				if (z != 0) {
					if (lineStart) {
						for (var y = 0; y < indents.Count; y++)
							if (indents[y].ind?.Length > 0)
								Print(indents[y].ind.AsMemory());
						for (var y = 0; y < indents.Count; y++)
							if (indents[y].ind2 != null)
								indents[y] = (indents[y].ind2, null);
					}
					lineStart = false;
					Print(z < 0 ? cs[x..] : cs.Slice(x, z));
				}
				lineStart = z >= 0 && cs.Span[x + z] == '\n';
				if (lineStart)
					Print(cs.Slice(x + z, 1));
				x += z + 1;
			}
		}
		Print();
		GetStringBuilder().Clear();
	}

	protected virtual void Print(ReadOnlyMemory<char>? s) => output.Write(s);
	protected virtual void Print() => output.Flush();

	public override void WriteLine() { base.WriteLine(); Flush(); }

	public override void WriteLine(bool value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(char value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(char[] buffer) { base.WriteLine(buffer); Flush(); }

	public override void WriteLine(char[] buffer, int index, int size)
	{
		base.WriteLine(buffer, index, size); Flush();
	}

	public override void WriteLine(decimal value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(double value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(int value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(long value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(object value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(float value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(string value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(string format, object arg0)
	{
		base.WriteLine(format, arg0); Flush();
	}

	public override void WriteLine(string format, object arg0, object arg1)
	{
		base.WriteLine(format, arg0, arg1); Flush();
	}

	public override void WriteLine(string format, object arg0, object arg1, object arg2)
	{
		base.WriteLine(format, arg0, arg1, arg2); Flush();
	}

	public override void WriteLine(string format, params object[] arg)
	{
		base.WriteLine(format, arg); Flush();
	}

	public override void WriteLine(uint value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(ulong value) { base.WriteLine(value); Flush(); }

	public override void WriteLine(ReadOnlySpan<char> buffer) { base.WriteLine(buffer); Flush(); }

	public override void WriteLine(StringBuilder value) { base.WriteLine(value); Flush(); }
}
