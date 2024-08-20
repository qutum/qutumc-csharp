//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace qutum;

public static class Extension
{
	public static ArraySegment<T> Seg<T>(this T[] s) => new(s);

	public static ArraySegment<T> Seg<T>(this T[] s, int from, int to) => new(s, from, to - from);

	public static MemoryEnum<T> Enum<T>(this ReadOnlyMemory<T> s) => new(s);

	public static ReadOnlyMemory<char> Mem(this string s) => s.AsMemory();

	public static ReadOnlyMemory<char> Mem(this string s, int from, int to) => s.AsMemory(from, to - from);

	public static bool Adds<T>(this ISet<T> s, IEnumerable<T> add)
	{
		if (add == null) return false;
		var z = s.Count; s.UnionWith(add); return s.Count > z;
	}

	public static IEnumerable<(T d, int x)> Each<T>(this IEnumerable<T> s, int offset = 0)
	{
		foreach (var d in s) yield return (d, offset++);
	}

	public static T? FirstOrNull<T>(this IEnumerable<T> s, Func<T, bool> If) where T : struct
	{
		foreach (var d in s) if (If(d)) return d;
		return null;
	}
}

public readonly struct MemoryEnum<T>(ReadOnlyMemory<T> mem) : IEnumerable<T>
{
	public IEnumerator<T> GetEnumerator() => new Enum { mem = mem };

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	struct Enum : IEnumerator<T>
	{
		internal ReadOnlyMemory<T> mem;
		int x = -1;
		public Enum() { }
		public readonly T Current => mem.Span[x];
		readonly object IEnumerator.Current => Current;
		public bool MoveNext() => ++x < mem.Length;
		public void Reset() => x = -1;
		public readonly void Dispose() { }
	}
}

public class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
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
		if (prev != null)
			prev.next = next;
		if (next != null)
			next.prev = prev;
		if (up?.head == this)
			up.head = next;
		if (up?.tail == this)
			up.tail = prev;
		if (clear)
			up = prev = next = null;
		return (T)this;
	}

	public T Dump(object extra = null, int after = 1)
	{
		bool first = prev == null && (up == null || after <= 0);
		bool last = next == null && (up == null || after > 0);
		string noInd = up == null && after == 0 ? "" : null;
		int o = dumpOrder;
		for (var t = head; ; t = t.next) {
			if (t == (o > 0 ? head : o < 0 ? null : head?.next))
				using (var env = EnvWriter.Indent
					(noInd ?? (after > 0 ? first ? "- " : "\\ " : last ? "- " : "/ ")))
					env.WriteLine(ToString(extra));
			if (t == null)
				break;
			int After = o > 0 || o == 0 && t != head ? 1 : -1; // sub after this
			using (var env = EnvWriter.Indent(noInd ?? ((After > 0 ? last : first) ? "  " : "| ")))
				t.Dump(extra, After);
		}
		if (up == null && prev == null)
			for (var t = next; t != null; t = t.next)
				t.Dump(extra, after);
		return (T)this;
	}

	// preorder >0, inorder 0, postorder <0
	public virtual int dumpOrder => 1;

	public virtual string ToString(object extra) => extra?.ToString() ?? "dump";

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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0250:Make struct 'readonly'")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0251:Make member 'readonly'")]
public struct StrMaker
{
	readonly StringBuilder s = new();

	public StrMaker() { }
	public static implicit operator StrMaker(string s) => new StrMaker() + s;
	public static implicit operator string(StrMaker s) => s.ToString();

	public static StrMaker operator +(StrMaker s, object d)
	{ if (d is not StrMaker m || m.s != s.s) s.s.Append(d); return s; }
	public StrMaker Join(object d) => s.Length > 0 ? this + d : this;

	public int Size => s.Length;
	public override string ToString() => s.ToString();
}

public class EnvWriter : StringWriter, IDisposable
{
	static readonly EnvWriter env = new();

	TextWriter output;
	bool pressKey;
	readonly List<string> indents = [];
	bool lineStart = true;

	public static EnvWriter Begin(bool pressKey = false)
	{
		env.pressKey = pressKey;
		if (env.indents.Contains(null))
			return env;
		Console.OutputEncoding = new UTF8Encoding(false, false);
		env.output = Console.Out;
		Console.SetOut(env);
		env.indents.Add(null);
		return env;
	}

	public static EnvWriter Use() => Indent("");

	public static EnvWriter Indent(string ind = "\t")
	{
		env.indents.Add(ind ?? throw new ArgumentNullException(nameof(ind)));
		return env;
	}

	protected override void Dispose(bool _)
	{
		if (indents.Count == 0)
			return;
		var ind = indents[^1];
		indents.RemoveAt(indents.Count - 1);
		if (ind == null) {
			Console.SetOut(output);
			try {
				if (pressKey && !Console.IsInputRedirected
						&& GetConsoleProcessList(procs, 1) == 1) {
					Write("Press Any Key ... "); Flush();
					Console.ReadKey(true);
				}
			}
			catch (DllNotFoundException) { }
		}
	}

	public override void Flush()
	{
		base.Flush();
		foreach (var cs in GetStringBuilder().GetChunks()) {
			int f = 0, t = 0;
			while (t >= 0 && f < cs.Length) {
				t = cs.Span[f..].IndexOfAny('\n', '\r');
				if (t != 0) {
					if (lineStart)
						foreach (var ind in indents)
							if (ind != null && ind.Length > 0)
								Print(ind.AsMemory());
					lineStart = false;
					Print(t < 0 ? cs[f..] : cs.Slice(f, t));
				}
				lineStart = t >= 0 && cs.Span[f + t] == '\n';
				if (lineStart)
					Print(cs.Slice(f + t, 1));
				f += t + 1;
			}
		}
		Print(null);
		GetStringBuilder().Clear();
	}

	void Print(ReadOnlyMemory<char>? s)
	{
		if (s != null)
			output.Write(s);
		else
			output.Flush();
		if (Debugger.IsAttached)
			if (s != null)
				Debug.Write(s);
			else
				Debug.Flush();
	}

	public override void WriteLine()
	{
		base.WriteLine(); Flush();
	}

	public override void WriteLine(bool value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(char value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(char[] buffer)
	{
		base.WriteLine(buffer); Flush();
	}

	public override void WriteLine(char[] buffer, int index, int size)
	{
		base.WriteLine(buffer, index, size); Flush();
	}

	public override void WriteLine(decimal value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(double value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(int value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(long value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(object value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(float value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(string value)
	{
		base.WriteLine(value); Flush();
	}

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

	public override void WriteLine(uint value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(ulong value)
	{
		base.WriteLine(value); Flush();
	}

	public override void WriteLine(ReadOnlySpan<char> buffer)
	{
		base.WriteLine(buffer); Flush();
	}

	public override void WriteLine(StringBuilder value)
	{
		base.WriteLine(value); Flush();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054")]
	[DllImport("kernel32.dll", SetLastError = false)]
	static extern uint GetConsoleProcessList(uint[] procs, uint n);
	static readonly uint[] procs = new uint[1];
}
