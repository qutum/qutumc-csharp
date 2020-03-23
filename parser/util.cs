//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace qutum
{
	public static class Extension
	{
		public static void Each<T>(this IEnumerable<T> s, Action<T, int> a)
		{
			int x = 0; foreach (var t in s) a(t, x++);
		}
	}

	public class LinkTree<T> : IEnumerable<T> where T : LinkTree<T>
	{
		public T up, prev, next, head, tail;

		public T Add(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
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

		public T AddHead(T sub)
		{
			if (sub == null)
				return (T)this;
			Debug.Assert(sub.up == null);
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

		public T AddNext(T next)
		{
			if (next == null)
				return (T)this;
			Debug.Assert(up == null && next.up == null && next.prev == null);
			var end = (T)this;
			while (end.next != null)
				end = end.next;
			(next.prev = end).next = next;
			return (T)this;
		}

		public T AddSub(T t)
		{
			if (t == null)
				return (T)this;
			var x = t.head;
			x.up = null;
			t.head = t.tail = null;
			return Add(x);
		}

		public T Remove(bool clear = true)
		{
			if (prev != null)
				prev.next = next;
			if (next != null)
				next.prev = prev;
			if (up != null && up.head == this)
				up.head = next;
			if (up != null && up.tail == this)
				up.tail = prev;
			if (clear) up = prev = next = null;
			return (T)this;
		}

		public T Dump(string ind = "", int pos = 0)
		{
			int f = 1, fix = 1;
			for (var x = head; ; x = x.next, f++) {
				if (f == fix)
					Console.WriteLine(DumpSelf(ind, pos < 0 ? "/ " : pos > 0 ? "\\ " : ""));
				if (x == null)
					break;
				x.Dump(pos == 0 ? ind : ind + (pos == (f < fix ? -2 : 2) ? "  " : "| "),
					f < fix ? x == head ? -2 : -1 : x == tail ? 2 : 1);
			}
			return (T)this;
		}

		public virtual string DumpSelf(string ind, string pos) => $"{ind}{pos} dump";

		public IEnumerator<T> GetEnumerator()
		{
			for (var x = head; x != null; x = x.next)
				yield return x;
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		class Backwarder : IEnumerable<T>
		{
			internal T tail;

			public IEnumerator<T> GetEnumerator()
			{
				for (var x = tail; x != null; x = x.prev)
					yield return x;
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		public IEnumerable<T> Backward() => new Backwarder { tail = tail };
	}

	class DebugWriter : StringWriter
	{
		internal TextWriter console;

		internal static void ConsoleBegin()
		{
			Console.OutputEncoding = new UTF8Encoding(false, false);
			if (Debugger.IsAttached && !(Console.Out is DebugWriter))
				Console.SetOut(new DebugWriter { console = Console.Out });
		}

		internal static void ConsoleEnd()
		{
			if (Debugger.IsAttached) {
				Console.WriteLine("Press Enter Key ...");
				Console.ReadLine();
			}
		}

		public override void Flush()
		{
			base.Flush();
			var s = GetStringBuilder().ToString();
			Debug.Write(s);
			Debug.Flush();
			console.Write(s);
			console.Flush();
			GetStringBuilder().Clear();
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

		public override void WriteLine(char[] buffer, int index, int count)
		{
			base.WriteLine(buffer, index, count); Flush();
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
	}
}
