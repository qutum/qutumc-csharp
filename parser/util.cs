//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace qutum.parser;

// typed int16 index
public struct X<T> : IComparable<X<T>>
{
	public short x;
	public readonly bool Yes => x != -1;
	public static readonly X<T> No = new() { x = -1 };

	public static explicit operator X<T>(short x) => new() { x = x };
	//	public static implicit operator short(X<T> x) => x.x;

	public static bool operator ==(X<T> a, X<T> b) => a.x == b.x;
	public static bool operator !=(X<T> a, X<T> b) => a.x != b.x;
	public static bool operator <(X<T> a, X<T> b) => a.x < b.x;
	public static bool operator >(X<T> a, X<T> b) => a.x > b.x;

	public override readonly string ToString() => x.ToString();
	public override readonly bool Equals(object that) => that is X<T> b && x == b.x;
	public override readonly int GetHashCode() => x.GetHashCode();
	public readonly int CompareTo(X<T> that) => x - that.x;
}

// jot of data to inclusive end, [on,to], default size is 1
public struct Jot
{
	public int on; // index on the first, [on]==first
	public int to; // inclusive index to the last, last==[to]

	public readonly int size => to - on + 1;
	public readonly bool any => on <= to;
	public readonly Range range => on..(to + 1);
	public readonly void Deconstruct(out int on, out int to) => (on, to) = (this.on, this.to);

	public static implicit operator Jot((int on, int to) j) => new() { on = j.on, to = j.to };
	public static implicit operator (int on, int to)(Jot j) => (j.on, j.to);
	public static implicit operator Jot(Jov j) => (j.on, j.via - 1);
}
// jot of data via exclusive end, [on,via), default size is 0
public struct Jov(int on, int via)
{
	public int on = on; // index on the first, [on]==first
	public int via = via; // exclusive index via the last, last==[via-1]

	public readonly int size => via - on;
	public readonly bool any => on < via;
	public readonly Range range => on..via;
	public readonly void Deconstruct(out int on, out int via) => (on, via) = (this.on, this.via);

	public static implicit operator Jov((int on, int via) j) => new() { on = j.on, via = j.via };
	public static implicit operator (int on, int via)(Jov j) => (j.on, j.via);
	public static implicit operator Jov(Jot j) => (j.on, j.to + 1);
}
public struct Jot<T>
{
	public T on, to;
	public readonly void Deconstruct(out T on, out T to) => (on, to) = (this.on, this.to);
	public static implicit operator Jot<T>((T on, T to) j) => new() { on = j.on, to = j.to };
}
public struct Jov<T>
{
	public T on, via;
	public readonly void Deconstruct(out T on, out T via) => (on, via) = (this.on, this.via);
	public static implicit operator Jov<T>((T on, T via) j) => new() { on = j.on, via = j.via };
}

public static partial class Extension
{
	public static bool IncLess(this ref int d, int z) => ++d < z || (d = z) < z;

	public static ArraySegment<T> Seg<T>(this T[] s) => new(s);
	public static ArraySegment<T> Seg<T>(this T[] s, Jov j) => new(s, j.on, j.size);

	public static IEnumerable<T> Enum<T>(this ReadOnlyMemory<T> s)
	{
		for (var x = 0; x < s.Length; x++) yield return s.Span[x];
	}

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

public struct BitSet<T> : IEnumerable<X<T>>
{
	public BitSet set;
	public readonly bool Empty => set.Empty;

	public BitSet<T> Use(int max) { set.Use(max); return this; }
	public readonly BitSet<T> Or(X<T> x) { set.Or(x.x); return this; }
	public readonly bool Or(BitSet<T> s) => set.Or(s.set);
	public readonly BitSet<T> Or(IEnumerable<X<T>> s)
	{
		foreach (var x in s ?? [])
			set.Or(x.x);
		return this;
	}
	public readonly BitSet<T> NewOr(BitSet<T> s, bool may = false)
	{
		var set = this.set.NewOr(s.set, may);
		return set.bits == this.set.bits ? this : new() { set = set };
	}
	public readonly bool Same(BitSet<T> s) => set.Same(s.set);
	public static BitSet<T> One(int max, X<T> x) => new BitSet<T>().Use(max).Or(x);

	public readonly X<T> Min() => (X<T>)short.CreateSaturating(set.Min());
	public readonly X<T> Max() => (X<T>)short.CreateSaturating(set.Max());
	public readonly IEnumerator<X<T>> GetEnumerator()
	{
		foreach (int x in set) yield return (X<T>)(short)x;
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
