<!--
Copyright 2008-2024 Qianyan Cai  
Under the terms of the GNU General Public License version 3  
http://qutum.com  http://qutum.cn
-->

## qutum.parser.MetaStr

元文法字符，支持空白、词、数字、量词、转义等

## qutum.parser.earley.Earley

语法解析器，输入指定`Lexer`，基于earley算法，输出语法树

支持量词、greedy串联贪婪匹配、优先分支、语法树节点保留或穿透、从错误位置或寻找后方词恢复

## qutum.parser.earley.EarleyStr

以`LerStr`为输入的earley语法解析器

## qutum.syntax.earley.Earley

未完成的旧版qutum语法解析器，输入`Lexier`，输出语法树，
