//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.syntax
{
	using Token = Token<Lex>;
	using Tokens = ArraySegment<Token<Lex>>;
	using Tree = Tree<ArraySegment<Token<Lex>>>;

	class Parsers : Parser<IEnumerable<byte>, Lex, Token, Tokens, Tree>
	{
		static readonly string grammer =
		@"|= EOL DED
		blocks = block+ skips?
		block = skips? line
		line = parse EOL inner? =+
		parse = BLANK* CONTENT CONTENT_BLANK* =+
		inner = skips? IND block+ skips? DED
		skips = skip+
		skip = empty|comm | IND skip+ DED | DED skip+ IND
		empty = SP* EOL =+
		comm = SP* COMM BLANK* EOL
		";

		public Parsers(Lexer l) : base(grammer, l)
		{
			treeKeep = false;
			treeExpect = 0;
			treeDumper = o => !(o is Tokens s) ? null
				: s.Count == 0 ? "" : string.Join(" ", s.Select(k => k.ToString()).ToArray());
		}

		public override Tree Parse(IEnumerable<byte> input)
		{
			scan.Load(input);
			var t = base.Parse(null);
			if (t.head is Tree x) {
			Loop: if (x.err == 0 && x.name == "empty" && x.up != t && x.next?.err == 0 && x.next?.name == "empty") {
					t.Add(new Tree { name = x.name, from = x.next.from, to = x.next.to, err = 1, expect = "too many empty lines" });
					while ((x = x.next).next?.err == 0 && x.next?.name == "empty") ;
				}
				if (x.head != null) { x = x.head; goto Loop; }
				else do if (x.next != null) { x = x.next; goto Loop; }
					while ((x = x.up) != null);
			}
			foreach (var k in ((Lexer)scan).errs)
				t.Add(new Tree {
					name = k.key.ToString(), from = k.from, to = k.to, err = 1, expect = k.value
				});
			scan.Unload();
			return t;
		}

		public override bool Check(IEnumerable<byte> input) => throw new NotImplementedException();
	}
}
