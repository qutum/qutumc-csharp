//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.syntax
{
	using Trees = Tree<ArraySegment<Token<Lex>>>;

	class Parsers : Parser<IEnumerable<byte>, Lex, Token<Lex>, ArraySegment<Token<Lex>>>
	{
		static string Grammar = @"|= Eol Ded
		blocks = block+ skips?
		block = skips? line
		line = parse Eol inner? =+
		parse = __* Parse Parse__* =+
		inner = skips? Ind block+ skips? Ded
		skips = skip+
		skip = empty|comm | Ind skip+ Ded | Ded skip+ Ind
		empty = _* Eol =+
		comm = _* Comm __* Eol
		";

		public Parsers(Lexer l) : base(Grammar, l) { treeKeep = false; treeExpect = 0; }

		public override Trees Parse(IEnumerable<byte> input)
		{
			scan.Load(input);
			var t = base.Parse(null);
			if (t.head is Trees x)
			{
				Loop: if (x.err < 0)
					x.Remove(false);
				else if (x.err == 0 && x.name == "empty")
				{
					if (x.up != t && x.next?.err == 0 && x.next?.name == "empty")
					{
						t.Add(new Trees
						{ name = x.name, from = x.next.from, to = x.next.to, err = 1, expect = "too many empty lines" });
						for (var y = x.next; (y = y.Remove(false).next)?.err == 0 && y?.name == "empty";) ;
					}
					x.Remove(false);
				}
				if (x.head != null) { x = x.head; goto Loop; }
				else do if (x.next != null) { x = x.next; goto Loop; }
					while ((x = x.up) != null);
			}
			scan.Tokens(0, scan.Loc()).Where(k => k.err).Select(k => t.Add(
				new Trees { name = k.key.ToString(), from = k.from, to = k.to, err = 1, expect = k.value })).Count();
			scan.Unload();
			return t;
		}

		public override bool Check(IEnumerable<byte> input) => throw new NotImplementedException();
	}
}
