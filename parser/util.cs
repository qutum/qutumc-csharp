//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
/// <summary>
/// Represents a typed index with a specific type.
/// </summary>
/// <typeparam name="X">The type of the index.</typeparam>
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

/// <summary>typed index, _ for name only, actual value is int</summary>
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
public struct X<_> : IUnaryPlusOperators<X<_>, int>, IComparisonOperators<X<_>, X<_>, bool>,
	IAdditionOperators<X<_>, int, X<_>>, IBitwiseOperators<X<_>, X<_>, X<_>>, IComparable<X<_>>
{
	int x;
	/// <summary>== 0</summary>
	public readonly bool zero => x == 0;
	/// <summary>> 0</summary>
	public readonly bool pos => x > 0;
	/// <summary>&lt; 0</summary>
	public readonly bool neg => x < 0;
	/// <summary>>= 0</summary>
	public readonly bool zpos => x >= 0;
	/// <summary>&lt;= 0</summary>
	public readonly bool zneg => x <= 0;
	/// <summary>!= -1</summary>
	public readonly bool yes => x != -1;
	/// <summary>== -1</summary>
	public readonly bool no => x == -1;
	/// <summary>0</summary>
	public static readonly X<_> Z = default;
	/// <summary>-1</summary>
	public static readonly X<_> No = (X<_>)(-1);

	/// <summary>int value to this index</summary>
	public static explicit operator X<_>(int x) => new() { x = x };
	/// <summary>int string</summary>
	public override readonly string ToString() => x.ToString();

	/// <summary>this index to int value</summary>
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

/// <summary>jot of data, [on,to] inclusive end, default size should be 1</summary>
/// <typeparam name="X">typed index (or int)</typeparam>
public struct Jot<X> : IEnum<X> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	/// <summary>index on the first, [on]==first</summary>
	public X on;
	/// <summary>inclusive index to the last, last==[to]</summary>
	public X to;

	/// <summary>to - on + 1, default is 1</summary>
	public readonly X size => to + -+on + 1;
	/// <summary>size > 0, also on &lt;= to</summary>
	public readonly bool any => +on <= +to;
	public readonly Range range => +on..+(to + 1);
	public readonly void Deconstruct(out X on, out X to) => (on, to) = (this.on, this.to);

	public static implicit operator Jot<X>((X on, X to) j) => new() { on = j.on, to = j.to };
	public static implicit operator (X on, X to)(Jot<X> j) => (j.on, j.to);
	public static implicit operator Jot<X>(Jov<X> j) => (j.on, j.via + -1);
	public static explicit operator Jot<int>(Jot<X> j) => (+j.on, +j.to);

	/// <summary>enumerate a copy of this jot</summary>
	public readonly IEnumerator<X> GetEnumerator()
	{
		X x = on; for (; +x < +to; x += 1) yield return x;
		if (+x == +to) yield return x; // no overflow
	}
}
/// <summary>jot of data, [on,via) exclusive end, default size should be 0</summary>
/// <typeparam name="X">typed index (or int)</typeparam>
public struct Jov<X> : IEnum<X> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	/// <summary>index on the first, [on]==first</summary>
	public X on;
	/// <summary>exclusive index via the last, last==[via-1]</summary>
	public X via;

	/// <summary>via - on, default is 0</summary>
	public readonly X size => via + -+on;
	/// <summary>size > 0, also on &lt; via</summary>
	public readonly bool any => +on < +via;
	public readonly Range range => +on..+via;
	public readonly void Deconstruct(out X on, out X via) => (on, via) = (this.on, this.via);

	public static implicit operator Jov<X>((X on, X via) j) => new() { on = j.on, via = j.via };
	public static implicit operator (X on, X via)(Jov<X> j) => (j.on, j.via);
	public static implicit operator Jov<X>(Jot<X> j) => (j.on, j.to + 1);
	public static explicit operator Jov<int>(Jov<X> j) => (+j.on, +j.via);

	/// <summary>enumerate a copy of this jot</summary>
	public readonly IEnumerator<X> GetEnumerator() { for (X x = on; +x < +via; x += 1) yield return x; }
}

/// <summary>array jot on typed index (or int). use 'ref this' to change the jot</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles")]
public struct Arr<X, T> : IEnum<T> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	/// <summary>actual array, may large than <see cref="j">jot</see></summary>
	public T[] s;
	/// <summary>jot</summary>
	public Jov<X> j;
	/// <summary>size, should &lt;= <see cref="s">actual array</see> length</summary>
	public readonly X size => j.size;
	/// <summary>read or write on typed index inside <see cref="j">jot</see></summary>
	public readonly ref T this[X x] { get => ref Span[+x]; }
	/// <summary>read or write on the last, == [^1]</summary>
	public readonly ref T last => ref Span[^1];
	/// <summary>read or write on backward typed index, == [^x]</summary>
	public readonly ref T Last(X x) => ref Span[^+x];

	/// <summary>empty array</summary>
	public Arr() => s = []; // used for Arr<X, T> a = []
	/// <summary>new array with size, jot is full</summary>
	public Arr(X size) : this(+size > 0 ? new T[+size] : []) { }
	/// <summary>new array of data, like [..s], jot is full</summary>
	public Arr(IEnumerable<T> s) : this([.. s]) { }
	/// <summary>new array with an actual one, jot is full</summary>
	public Arr(T[] s) { this.s = s; j = (default, default(X) + s.Length); }
	/// <summary>new array with size and a jot</summary>
	public Arr(X size, Jov<X> j) : this(+size > 0 ? new T[+size] : [], j) { }
	/// <summary>array with an actual one and a jot</summary>
	public Arr(T[] s, Jov<X> j) { this.s = s; this.j = j; _ = Span; }

	internal Arr<X, T> _Add(T d)
	{
		if (+j.via >= s.Length)
			Array.Resize(ref s, +j.via + (s.Length < 16384 ? int.Max(s.Length, 1024) : s.Length >> 1));
		s[+j.via] = d; j.via += 1;
		return this;
	}
	internal Arr<X, T> _Drop(X x) => _Drop((x, x + 1));
	internal Arr<X, T> _Drop(Jov<X> j_)
	{
		if (Jot(j_) is { j: var jj } && jj.any) {
			Array.Copy(s, +jj.via, s, +jj.on, +j.via - +jj.via);
			j = (j.on, j.via + -+jj.size);
		}
		return this;
	}
	internal Arr<X, T> _Resize(X size, bool keepJ = false) => _Resize(size, keepJ ? j : (default, size));
	internal Arr<X, T> _Resize(X size, Jov<X> j)
	{
		Array.Resize(ref s, +size); this.j = j; _ = Span; return this;
	}
	internal Arr<X, T> _Clear()
	{
		if (s.Length <= 1024) Array.Clear(s); else s = [];
		j = default; return this;
	}

	/// <summary>Span of array jot</summary>
	public readonly Span<T> Span => ((Span<T>)s)[j.range];
	/// <summary>array with jot inside this array jot</summary>
	public readonly Arr<X, T> Jot(Jov<X> j) => new(s, (this.j.on + +j.on, this.j.on + +j.via));
	/// <summary>array with jot inside this array jot</summary>
	public readonly Arr<X, T> Jot(int on, X via) => Jot((default(X) + on, via));

	/// <summary>enumerate this array of a jot copy</summary>
	public readonly IEnumerator<T> GetEnumerator()
	{
		for (int x = +j.on; x < +j.via; x++) yield return s[x];
	}
	/// <summary>enumerate this array of a jot copy, add each index by an offset</summary>
	public readonly IEnumerable<(T d, X x)> Each(X offset = default)
	{
		int o = +j.on; offset += -o;
		for (int x = o; x < +j.via; x++) yield return (s[x], offset + x);
	}

	/// <summary>use <see cref="Array.BinarySearch{T}(T[], T)"/> to search a data in this array jot.
	/// </summary>
	/// <returns>typed index of BinarySearch returned, if not found and xNo is true: -1</returns>
	public readonly X BinFind(T v, bool xNo = true) =>
		default(X) + (Array.BinarySearch(s, +j.on, +j.size, v) is var x && x < 0 && xNo ? -1 : x);
}
public static class ArrExtention
{
	extension<X, T>(ref Arr<X, T> s) where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
	{
		/// <summary>add a data after the last, resize the actual array if required. use 'ref this'</summary>
		public Arr<X, T> Ad(T d) => s = s._Add(d); // not 'Add' to deny [..data] to call each Add
		/// <summary>drop a data, move data else. use 'ref this'</summary>
		public Arr<X, T> Drop(X x) => s = s._Drop(x);
		/// <summary>drop a jot of data, move data else. use 'ref this'</summary>
		public Arr<X, T> Drop(Jov<X> j) => s = s._Drop(j);
		/// <summary>resize the actual array, unchange the jot or set it full. use 'ref this'</summary>
		public Arr<X, T> Resize(X size, bool keepJ = false) => s = s._Resize(size, keepJ);
		/// <summary>resize the actual array, set jot. use 'ref this'</summary>
		public Arr<X, T> Resize(X size, Jov<X> j) => s = s._Resize(size, j);
		/// <summary>drop all data, zero jot. use 'ref this'</summary>
		public Arr<X, T> Clear() => s = s._Clear();
	}
}

/// <summary>array jot on typed index (or int). no need 'ref this' vs <see cref="Arr{X, T}"/></summary>
public class List<X, T> : IEnum<T> where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
{
	Arr<X, T> s;
	/// <summary>size, should &lt;= actual array length</summary>
	public X size => s.size;
	/// <summary>read or write on typed index inside jot</summary>
	public ref T this[X x] { get => ref s[x]; }
	/// <summary>read or write on the last, == [^1]</summary>
	public ref T last => ref s.last;
	/// <summary>read or write on backward typed index, == [^x]</summary>
	public ref T Last(X x) => ref s.Last(x);

	/// <summary>new array with size, jot is full</summary>
	public List(X size = default) => s = new(size);
	/// <summary>new array of data, like [..s], jot is full</summary>
	public List(IEnumerable<T> ds) => s = new(ds);
	/// <summary>add a data after the last, resize the actual array if required</summary>
	public List<X, T> Ad(T d) { s.Ad(d); return this; } // not 'Add' to deny [..data] to call each Add
	/// <summary>drop a data, move data else</summary>
	public List<X, T> Drop(X x) { s.Drop(x); return this; }
	/// <summary>drop a jot of data, move data else</summary>
	public List<X, T> Drop(Jov<X> j) { s.Drop(j); return this; }
	/// <summary>drop all data, zero jot</summary>
	public virtual List<X, T> Clear() { s.Clear(); return this; }

	/// <summary>enumerate this array, jot end may change</summary>
	public IEnumerator<T> GetEnumerator()
	{
		for (int x = +s.j.on; x < +s.j.via; x++) yield return s.s[x]; // s.j.via may change
	}
	/// <summary>enumerate this array, add each index by an offset, jot end may change</summary>
	public IEnumerable<(T d, X x)> Each(X offset = default)
	{
		int o = +s.j.on; offset += -o;
		for (int x = o; x < +s.j.via; x++) yield return (s.s[x], offset + x); // s.j.via may change
	}
}

public static partial class Extension
{
	/// <summary>cast int jot to typed jot</summary>
	public static Jot<X<_>> X<_>(this Jot j) => ((X<_>)j.on, (X<_>)j.to);
	/// <summary>cast int jot to typed jot</summary>
	public static Jov<X<_>> X<_>(this Jov j) => ((X<_>)j.on, (X<_>)j.via);

	public static X<_> Min<_>(this X<_> a, X<_> b) => a < b ? a : b;
	public static X<_> Max<_>(this X<_> a, X<_> b) => a < b ? b : a;
	/// <summary>increase by 1 if &lt; z, so max is z</summary><returns>increased &lt; z</returns>
	public static bool IncLess(this ref int d, int z) => d < z && ++d < z;
	/// <summary>increase by 1 if &lt; z, so max is z</summary><returns>increased &lt; z</returns>
	public static bool IncLess<_>(this ref X<_> d, int z) => +d < z && +(d += 1) < z;

	public static bool Adds<T>(this ISet<T> s, IEnumerable<T> add)
	{
		if (add == null) return false;
		var z = s.Count; s.UnionWith(add); return s.Count > z;
	}

	extension<T>(IEnumerable<T> s)
	{
		/// <summary>enumerate data, add each index by an offset</summary>
		public IEnumerable<(T d, int x)> Each(int offset = 0)
		{
			foreach (var d in s) yield return (d, offset++);
		}

		/// <summary>same as <see cref="Arr{X, T}.Arr(IEnumerable{T})"/>, _ for type inference only</summary>
		public Arr<X, T> ToArr<X>(X _ = default)
			where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X> => new(s);

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
		public Dictionary<T, int> ToCount() => s.ToDict<T, int>((d, z) => z + 1);
	}

	extension<T>(IEnumerable<T> s) where T : struct
	{
		/// <summary>enumerate data, append null if data is empty</summary>
		public IEnumerable<T?> May()
		{
			bool no = true;
			foreach (var d in s) { no = false; yield return d; }
			if (no) yield return null;
		}
		/// <returns>the first checked data, or null</returns>
		public T? FirstOrNull(Func<T, bool> If)
		{
			foreach (var d in s) if (If(d)) return d;
			return null;
		}
	}
}

/// <summary>tree node with four direction uncyclic links</summary>
public partial class LinkTree<T> : IEnum<T> where T : LinkTree<T>
{
	/// <summary>parent node. if null, this is root or one root of forest</summary>
	public T up;
	/// <summary>previous sibling node.
	/// if not null: past.next == this, if null and up not null: up.head == this</summary>
	public T past;
	/// <summary>next sibling node.
	/// if not null: next.past == this, if null and up not null: up.tail == this</summary>
	public T next;
	/// <summary>head of sub nodes.
	/// if not null: head.up == this && head.past == null && tail != null, if null: tail == null</summary>
	public T head;
	/// <summary>tail of sub nodes.
	/// if not null: tail.up == this && tail.next == null && head != null, if null: head == null</summary>
	public T tail;

	/// <summary>the most past sibling. if up not null: == up.head</summary>
	public T First()
	{
		if (up != null)
			return up.head;
		var t = (T)this;
		while (t.past != null)
			t = t.past;
		return t;
	}

	/// <summary>the most next sibling. if up not null: == up.tail</summary>
	public T Last()
	{
		if (up != null)
			return up.tail;
		var t = (T)this;
		while (t.next != null)
			t = t.next;
		return t;
	}

	/// <summary>add t and all siblings to after this.tail. t must be first root</summary>
	public T Add(T t)
	{
		if (t == null)
			return (T)this;
		Debug.Assert(t.up == null && t.past == null);
		var end = t;
		for (end.up = (T)this; end.next != null; end.up = (T)this)
			end = end.next;
		if (head == null)
			head = t;
		else
			(t.past = tail).next = t;
		tail = end;
		return (T)this;
	}

	/// <summary>add t and all siblings to before this.head. t must be first root</summary>
	public T AddHead(T t)
	{
		if (t == null)
			return (T)this;
		Debug.Assert(t.up == null && t.past == null);
		var end = t;
		for (end.up = (T)this; end.next != null; end.up = (T)this)
			end = end.next;
		if (tail == null)
			tail = end;
		else
			(end.next = head).past = end;
		head = t;
		return (T)this;
	}

	/// <summary>cut all t subs from t, add them to after this.tail</summary>
	public T AddSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		var x = t.head;
		t.head = t.tail = null;
		x.up = null;
		return Add(x);
	}

	/// <summary>append t and all siblings to after this.Last. this must be root, t must be first root</summary>
	public T Append(T t)
	{
		if (t == null)
			return (T)this;
		Debug.Assert(up == null && t.up == null && t.past == null);
		(t.past = Last()).next = t;
		return (T)this;
	}

	/// <summary>cut all t subs from t, add them to after this.Last. this must be root</summary>
	public T AppendSubOf(T t)
	{
		if (t?.head == null)
			return (T)this;
		Debug.Assert(up == null);
		var x = t.head;
		t.head = t.tail = null;
		(x.past = Last()).next = x;
		for (x.up = null; x.next != null; x.up = null)
			x = x.next;
		return (T)this;
	}

	/// <summary>cut this from up and siblings, to be a single root</summary>
	public T Cut(bool keepLink = false)
	{
		if (past == null)
			up?.head = next;
		if (next == null)
			up?.tail = past;
		past?.next = next;
		next?.past = past;
		if (!keepLink)
			up = past = next = null;
		return (T)this;
	}

	/// <summary>enumerate subs from head to tail</summary>
	public IEnumerator<T> GetEnumerator()
	{
		for (var x = head; x != null; x = x.next)
			yield return x;
	}

	/// <summary>enumerate subs backward from tail to head</summary>
	public IEnumerable<T> Backward()
	{
		for (var x = tail; x != null; x = x.past)
			yield return x;
	}
}

public struct BitSet : IEnum<int>
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
}

// meaningless _ is for distinguish only
public struct BitSet<_> : IEnum<X<_>>
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
}

public interface IEnum<out T> : IEnumerable<T> where T : allows ref struct
{
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public readonly struct StrMake
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
