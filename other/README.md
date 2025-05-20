<!--
Copyright 2008-2025 Qianyan Cai
Under the terms of the Creative Commons BY-SA 4.0 http://creativecommons.org/licenses/by-sa/4.0/deed.en
http://qutum.com  http://qutum.cn
-->

## qutum.parser.meta.MetaStr

元文法字符，支持空白、词、数字、量词、转义等

## qutum.parser.meta.MetaLex

`qutum.parser.Lexier`元文法
- 以空格分隔多组，以`|`分隔多分支，`|`左接空格为空分支，`|`右接的空格忽略
- 每组开头`*`为重试，每分支开头`+`为循环，每字节后接`+`为重复

## qutum.parser.earley.Earley

语法解析器，读取`Lexer`，基于earley算法，产出语法树

支持量词、greedy串联贪婪匹配、优先分支、语法树节点保留或穿透、从错误位置或寻找后方词恢复

## qutum.parser.earley.EarleyStr

读取`LerStr`的earley语法解析器
